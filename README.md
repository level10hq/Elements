## THE HYPAR SDK IS CURRENTLY IN BETA. DO NOT USE IT FOR PRODUCTION WORK.

# Hypar SDK
[![Build Status](https://travis-ci.org/hypar-io/sdk.svg?branch=master)](https://travis-ci.org/hypar-io/sdk)

The Hypar SDK is a library for creating functions that execute on Hypar.

- `Hypar.Elements` provides abstractions for building elements like beams and slabs.
- `Hypar.Geometry` provides a minimal geometry library that supports points, lines, curves, and extrusions.

The Hypar SDK also reads and writes data using several open standards like [GeoJson](http://geojson.org/) and [glTF](https://www.khronos.org/gltf/).

## Getting Started
- The Hypar SDK is currently in beta. Contact beta@hypar.io to have an account created. Functions can be authored and executed locally. A login is only required when publishing your function to the world!
- Install [.NET](https://www.microsoft.com/net/)
- Install the [Hypar CLI](https://github.com/hypar-io/sdk/tree/master/src/cli).
```
hypar new <function name>
cd <function name>
```
- Edit the `hypar.json` file to describe your function.
```
hypar publish
```
- Preview `.glb` models generated by Hypar locally using the [glTF Extension for Visual Studio Code](https://github.com/AnalyticalGraphicsInc/gltf-vscode), or [Don McCurdy's online glTF Viewer](https://gltf-viewer.donmccurdy.com/).

## Configuration
The Hypar CLI will create a `hypar.json` file in your function's directory. This file is used to provide configuration information to Hypar. Here's an example of a `hypar.json` file.
```json
{
  "function": "box.box",
  "runtime": "dotnetcore2.0",
  "parameters": {
    "height": {
      "description": "The height of the box.",
      "max": 11,
      "min": 1,
      "step": 5,
      "type": "number"
    },
    "length": {
      "description": "The length of the box.",
      "max": 11,
      "min": 1,
      "step": 5,
      "type": "number"
    },
    "width": {
      "description": "The width of the box.",
      "max": 11,
      "min": 1,
      "step": 5,
      "type": "number"
    }
  },
  "returns": {
    "volume": {
      "description": "The volume of the box."
    }
  },
  "version": "0.0.1"
}
```
|Property|Description
|:--|:--
|`function`|The fully qualified name of the function. For python functions this will be the `module.function`. For .net functions this will be `namespace.class.method`.
|`runtime`|At this time only `dotnetcore2.0` is supported.
|`parameters`|An object containing data about each parameter.
|`description`|A description of the parameter. This description will show up in the Hypar web application.
|`max`|The maximum value for a parameter.
|`min`|The minimum value for a parameter.
|`step`|The value by which the parameter will be incremented when multiple executions are requested.
|`type`|The type of parameter. Supported values are `number`, `location`, and `point`.
|`version`|The version of the function. Versions should adhere to [semantic versioning](https://semver.org/).

## Examples
The best examples are those provided in the [tests](https://github.com/hypar-io/elements/tree/master/test), where we demonstrate usage of almost every function in the library.

## Build
`dotnet build`

## Test
`dotnet test`

## Third Party Libraries

- [LibTessDotNet](https://github.com/speps/LibTessDotNet)  
- [Verb](https://github.com/pboyer/verb)
