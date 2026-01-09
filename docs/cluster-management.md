# Cluster Management

## EventEnabled

```mermaid
flowchart LR

subgraph UI1
  inputButton[Button]
end

subgraph UI2
  outputBox[Box]
end

subgraph FunctionBlock
  input[InputConnector]-->logic{Logic}
  logic-->output[OutputConnector]
end

subgraph Broker
  inputTopic[InputConnector ID]
  outputTopic[OutputConnector ID]
end

inputButton-->inputTopic
inputTopic-->input
output-->outputTopic
outputTopic-->outputBox

classDef input stroke:green
classDef output stroke:blue

class inputTopic,input input
class outputTopic,output output
```

## Pooling

Pooling requires separate links to combine values into a single value before passing it to an input. [EventEnabled](#eventenabled) input connectors only use one link regardless of the amout of input controls.

```mermaid
flowchart LR

subgraph UI
  button[Button]
  inputBox[InputBox]
end

subgraph Remote FunctionBlock
  output1[OutputConnector1]
  output2[OutputConnector2]
end

subgraph Broker
  topic1[InputConnector ID]
  topic2[OutputConnector1 ID]
  topic3[OutputConnector2 ID]
end

subgraph Local FunctionBlock
  pooling[Pooling]
  input[InputConnector]
end

button-->topic1
inputBox-->topic1
output1-->topic2
output2-->topic3
topic1-->pooling
topic2-->pooling
topic3-->pooling
pooling-->input

classDef input stroke:green
classDef output stroke:blue

class topic1,input input
class topic2,topic3,output1,output2 output
```
