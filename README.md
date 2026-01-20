# CubiCasa.NET

A pure, unopinionated .NET parser for the CubiCasa5k dataset.

## Introduction

CubiCasa.NET is a library designed to load and parse the CubiCasa5k dataset, which consists of 5000 floorplan images with SVG annotations. This library provides a raw data loader that faithfully represents the source data structures in C#, including SVG geometry parsing and metadata handling.

## Installation

This package is available via GitHub Packages.

```xml
<PackageReference Include="CubiCasa.NET" Version="0.1.0-alpha.1" />
```

(Note: Replace version with the latest available version)

## Getting the Data

The CubiCasa5k dataset is hosted on Zenodo. It is not included in this repository due to its size (~5.5 GB).

To download and setup the dataset:

1.  Navigate to the `scripts` folder.
2.  Install python dependencies: `pip install -r requirements.txt`.
3.  Run the downloader: `python download_dataset.py`.

This will download the dataset to the `data/` folder in the repository root.

## Usage

### Loading a Single Floor (SVG)

```csharp
using CubiCasa;

var loader = new CubiCasaLoader();
var floor = loader.LoadFloor("path/to/data/cubicasa5k/high_quality/123/model.svg");

Console.WriteLine($"Width: {floor.WidthPixels}, Height: {floor.HeightPixels}");
foreach (var entity in floor.Entities)
{
    Console.WriteLine($"Entity: {entity.Type}, ID: {entity.OriginalId}");
}
```

### Loading a Building (Folder)

```csharp
using CubiCasa;

var loader = new CubiCasaLoader();
var building = loader.LoadBuilding("path/to/data/cubicasa5k/high_quality/123/");

Console.WriteLine($"Building ID: {building.BuildingId}");
foreach (var floor in building.Floors)
{
    Console.WriteLine($"Floor {floor.FloorIndex}: {floor.WidthPixels}x{floor.HeightPixels}");
}
```

## Linux Support

This library is fully cross-platform and does not require `libgdiplus` or `System.Drawing.Common`, as it uses a custom lightweight SVG parser tailored for the CubiCasa dataset.
