using Bunit;

namespace ClusterManagement.Client.Tests;

public static class BunitJSInteropExtensions
{
    extension(BunitJSInterop interop)
    {
        /// <summary>
        /// Copied call from suite client tests
        /// </summary>
        /// <returns></returns>
        public BunitJSInterop ConfigureQuickGridJSInterop()
        {
            interop.Mode = JSRuntimeMode.Loose;

            var rootModule = interop.SetupModule("./_content/Microsoft.AspNetCore.Components.QuickGrid/QuickGrid.razor.js");
            rootModule.Mode = JSRuntimeMode.Loose;

            return interop;
        }
    }
}
