# Develop an Akri Connector

## Development paths

There are two main options for creating an Akri Connector, depending on the level on control required and the complexity of the solution:

| Options | Pros | Cons |
|-|-|-|
| **Template** | Simplified implementation requiring implementation of specific interfaces. Much of the workflow complexity is abstracted away | Less flexibility on how the Connector operates and maybe restrictive for more complex scenarios. |
| **Custom** | Full control over the layout and implementation, which is best for complex scenarios. | Additional complexity of managing the Connectors workflow so that Assets are correctly managed |

### Template

A number of templates across the different languages are provided to simplify the development of a new Akri Connector. The template extrapolates away much of the complexity and scaffolding required so the developer can focus on the specific components of asset discovery and protocol translation.

1. [TODO] .NET template link

### Custom

A custom Akri Connector will directly utilize the various clients of Azure IoT Operations SDKs to achieve the required outcome. This flow provides the maximum amount of flexibility in the construction of the application, and would be useful where the available templates do not provide the required functionality.

## Setup

Follow the [Setup instruction](../setup.md) to setup and configure your cluster for developing with Azure IoT Operations.

## Deployment

An Akri connector is usually deployed via the Akri operator, however for development it can be instantiated directly on your local machine.