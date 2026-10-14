using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ClusterManagement.Client.Connections.Localization;
using ClusterManagement.Internal.Contracts;
using ClusterManagement.Internal.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.Client.Models;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.Button.Enums;
using ViciOne.Ui.Blazor.Components.Button.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;
using CommonLocalization = ClusterManagement.Client.Localization.Common;

namespace ClusterManagement.Client.Connections;

/// <summary>
/// Lists the certificates in a certificate store of an OPC UA connection, deletes them and, if allowed, uploads new ones.
/// </summary>
/// <remarks>
/// The certificates are changed on the server right away, not when the connection settings dialog is saved.
/// </remarks>
[SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "An OPC UA application URI is not a resolvable URL, it is passed on as the connection stores it.")]
public sealed partial class OpcUaCertificateStoreField
{
    // every extension a certificate file commonly has, the backend accepts any DER or PEM encoded certificate
    private const string CertificateFileExtensions = ".der,.cer,.crt,.pem";
    // an application certificate needs its private key, which PKCS#12 and PEM files can hold
    private const string ApplicationCertificateFileExtensions = ".pfx,.p12,.pem";

    private readonly string _deleteIconCssClass = MonochromeIconName.Delete.GetCssClasses(ButtonSize.Small.ToMonochromeIconSize()).ToSpaceSeparated();
    private readonly string _uploadIconCssClasses = MonochromeIconName.Export.GetCssClasses(MonochromeIconSize.SmallMedium).ToSpaceSeparated();

    private IReadOnlyCollection<OpcUaCertificateInfo> _certificates = [];
    private IReadOnlyCollection<string> _warnings = [];
    private string? _loadedStorePath;
    private bool _isLoading;
    private bool _isBusy;
    private string? _error;
    private OpcUaCertificateInfo? _pendingDelete;
    private string? _password;

    private SettingsFieldFileUpload<OpcUaCertificateUpload>? _fileUpload;
    // The upload field cancels a running upload when it gets a filename or ticket other than its own, so both are passed back as it reports them.
    private string? _uploadFilename;
    private IUploadTicket? _uploadTicket;

    /// <summary>Gets or sets the label of the store.</summary>
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets a description of what the store is for, shown below it.</summary>
    [Parameter] public string? Subline { get; set; }

    /// <summary>Gets or sets the store path as the connection stores it.</summary>
    [Parameter, EditorRequired] public string? StorePath { get; set; }

    /// <summary>Gets or sets whether certificates can be uploaded to the store.</summary>
    [Parameter] public bool AllowUpload { get; set; }

    /// <summary>
    /// Gets or sets whether the store holds the connection's application certificate: an upload replaces it and needs its
    /// private key, and the certificate's subject and URI are shown.
    /// </summary>
    [Parameter] public bool IsApplicationStore { get; set; }

    /// <summary>Gets or sets the connection's application certificate subject, which an uploaded application certificate is checked against.</summary>
    [Parameter] public string? ApplicationCertificateSubject { get; set; }

    /// <summary>Gets or sets the connection's application URI, which an uploaded application certificate is checked against.</summary>
    [Parameter] public string? ApplicationUri { get; set; }

    /// <summary>Gets whether the store has certificates, as far as they are loaded.</summary>
    public bool HasCertificates => _certificates.Count > 0;

    [Inject] private IUiMediator Mediator { get; set; } = default!;
    [Inject] private IUploadTicketFactory UploadTicketFactory { get; set; } = default!;
    [Inject] private IStreamUploadHandler<OpcUaCertificateUpload> StreamUploadHandler { get; set; } = default!;
    [Inject] private ILogger<OpcUaCertificateStoreField> Logger { get; set; } = default!;

    // a store holds one application certificate, another one is only uploaded once the current one is deleted
    private bool IsUploadLocked => IsApplicationStore && HasCertificates;

    private string AcceptedFileExtensions => IsApplicationStore ? ApplicationCertificateFileExtensions : CertificateFileExtensions;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (StorePath == _loadedStorePath)
            return;

        _loadedStorePath = StorePath;
        _pendingDelete = null;
        _error = null;
        _warnings = [];

        _isLoading = true;
        await LoadCertificatesAsync();
        _isLoading = false;
    }

    /// <summary>
    /// Deletes every certificate of the store, e.g. an application certificate that no longer fits the connection.
    /// </summary>
    public async Task DeleteAllAsync()
    {
        _pendingDelete = null;

        foreach (var certificate in _certificates.ToList())
            await DeleteAsync(certificate);

        StateHasChanged();
    }

    private async Task LoadCertificatesAsync()
    {
        if (string.IsNullOrEmpty(StorePath))
        {
            _certificates = [];
            return;
        }

        try
        {
            var response = await Mediator.Request<GetOpcUaCertificatesRequest, GetOpcUaCertificatesResponse>(new(StorePath), CancellationToken.None);

            _certificates = response.Certificates;
            if (response.RequestError is not null)
                _error = OpcUaConnectionSettings.CertificatesLoadFailed;
        }
        catch (Exception e)
        {
            LogRequestError(Logger, e, nameof(GetOpcUaCertificatesRequest), StorePath);
            _certificates = [];
            _error = OpcUaConnectionSettings.CertificatesLoadFailed;
        }
    }

    private async Task DeleteConfirmedAsync()
    {
        if (_pendingDelete is not { } certificate)
            return;

        _pendingDelete = null;
        await DeleteAsync(certificate);
    }

    private async Task DeleteAsync(OpcUaCertificateInfo certificate)
    {
        if (string.IsNullOrEmpty(StorePath))
            return;

        _isBusy = true;
        _error = null;
        _warnings = [];
        try
        {
            var response = await Mediator.Request<DeleteOpcUaCertificateRequest, DeleteOpcUaCertificateResponse>(new(StorePath, certificate.Thumbprint), CancellationToken.None);

            if (response.RequestError is null)
            {
                // the store is known to have changed only by this, so the list is updated without asking the device again
                _certificates = [.. _certificates.Where(c => c != certificate)];
                return;
            }

            _error = OpcUaConnectionSettings.CertificateDeleteFailed;
        }
        catch (Exception e)
        {
            LogRequestError(Logger, e, nameof(DeleteOpcUaCertificateRequest), StorePath);
            _error = OpcUaConnectionSettings.CertificateDeleteFailed;
        }
        finally
        {
            _isBusy = false;
        }

        // the certificate may have been deleted by someone else in the meantime, then the list is outdated
        await LoadCertificatesAsync();
    }

    private void CancelDelete() => _pendingDelete = null;

    private Task OnUploadStart(IUploadTicket uploadTicket)
    {
        _uploadTicket = uploadTicket;
        _error = null;
        _warnings = [];
        return Task.CompletedTask;
    }

    private async Task OnUploadSuccessAsync(StreamUploadSuccessResult successResult)
    {
        // The certificate is on the server now, so the field is free for the next file while the backend adds it.
        await ResetUploadAsync();

        if (string.IsNullOrEmpty(StorePath))
            return;

        _isBusy = true;
        try
        {
            if (IsApplicationStore)
                await AddApplicationCertificateAsync(successResult.DestinationFile);
            else
                await AddCertificateAsync(successResult.DestinationFile);
        }
        catch (Exception e)
        {
            // whether the certificate was added is unknown, so the list is loaded again
            LogRequestError(Logger, e, IsApplicationStore ? nameof(AddOpcUaApplicationCertificateRequest) : nameof(AddOpcUaCertificateRequest), StorePath);
            _error = CommonLocalization.UploadFailed;
            await LoadCertificatesAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task AddCertificateAsync(string uploadedFile)
    {
        var response = await Mediator.Request<AddOpcUaCertificateRequest, AddOpcUaCertificateResponse>(new(StorePath!, uploadedFile), CancellationToken.None);

        if (response.RequestError is { } error)
        {
            // certificates of a chain stored before the failure stay in the store, so the list is loaded again
            _error = GetUploadErrorMessage(error.ErrorCode);
            await LoadCertificatesAsync();
        }
        else
        {
            AddToList(response.Certificates);
            if (response.IssuerCertificatesLeftOut)
                _warnings = [OpcUaConnectionSettings.IssuerCertificatesLeftOut];
        }
    }

    private async Task AddApplicationCertificateAsync(string uploadedFile)
    {
        var request = new AddOpcUaApplicationCertificateRequest(
            StorePath!, uploadedFile, string.IsNullOrEmpty(_password) ? null : _password, ApplicationCertificateSubject ?? string.Empty, ApplicationUri ?? string.Empty);
        var response = await Mediator.Request<AddOpcUaApplicationCertificateRequest, AddOpcUaApplicationCertificateResponse>(request, CancellationToken.None);

        if (response.RequestError is { } error)
        {
            _error = GetUploadErrorMessage(error.ErrorCode);
            return;
        }

        // the password is only needed for this file, it is not kept around for the next one
        _password = null;

        // the uploaded certificate replaces the one the store had
        if (response.Certificate is { } added)
            _certificates = [added];

        _warnings = [.. response.Warnings.Select(GetWarningMessage)];
    }

    // the response holds the added certificates, so the list is updated without asking the device again; the store keeps
    // a certificate only once, uploading it again returns the stored one
    private void AddToList(IEnumerable<OpcUaCertificateInfo> certificates)
        => _certificates = [.. _certificates.Concat(certificates)
            .DistinctBy(c => c.Thumbprint, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Subject, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Maps the backend error codes to localized messages; the technical details are logged by the backend.
    /// </summary>
    private static string GetUploadErrorMessage(int errorCode)
        => errorCode switch
        {
            OpcUaCertificateError.InvalidCertificate => OpcUaConnectionSettings.InvalidCertificate,
            OpcUaCertificateError.InvalidApplicationCertificate => OpcUaConnectionSettings.InvalidApplicationCertificate,
            OpcUaCertificateError.NoPrivateKey => OpcUaConnectionSettings.NoPrivateKey,
            _ => CommonLocalization.UploadFailed,
        };

    private static string GetWarningMessage(OpcUaApplicationCertificateWarning warning)
        => warning switch
        {
            OpcUaApplicationCertificateWarning.SubjectMismatch => OpcUaConnectionSettings.ApplicationCertificateSubjectMismatch,
            OpcUaApplicationCertificateWarning.ApplicationUriMismatch => OpcUaConnectionSettings.ApplicationCertificateUriMismatch,
            _ => warning.ToString(),
        };

    private async Task OnUploadErrorAsync(StreamUploadErrorResult errorResult)
    {
        await ResetUploadAsync();
        _error = errorResult.Message;
    }

    private Task OnUploadCancel()
    {
        // The field has already reset itself.
        _uploadTicket = null;
        return Task.CompletedTask;
    }

    private async Task ResetUploadAsync()
    {
        _uploadTicket = null;

        if (_fileUpload is not null)
            await _fileUpload.ResetAsync();
    }

    private static bool IsExpired(OpcUaCertificateInfo certificate)
        => certificate.NotAfter < DateTimeOffset.UtcNow;

    private static string GetValidityText(OpcUaCertificateInfo certificate)
    {
        var date = certificate.NotAfter.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);

        return string.Format(CultureInfo.CurrentCulture,
            IsExpired(certificate) ? OpcUaConnectionSettings.CertificateExpired : OpcUaConnectionSettings.CertificateValidUntil,
            date);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Request} failed for OPC UA certificate store '{StorePath}'")]
    private static partial void LogRequestError(ILogger logger, Exception exception, string request, string? storePath);
}
