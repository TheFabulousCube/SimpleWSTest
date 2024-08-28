# SimpleWSTest
# WebSocket Server and Client Installation Guide

## Overview

This project consists of a WebSocket server and a client that communicates with the server using WebSockets. The client is packaged into an MSI installer using WiX.

### Components

1. **WebSocket Server**: Implemented in `SimpleWS-Server\Program.cs`.
2. **WebSocket Client**: Implemented in `ClientWorkerService\Worker.cs` and set up in `ClientWorkerService\Program.cs`.
3. **MSI Installer**: Defined in `ClientInstaller\Package.wxs` and built using `ClientInstaller.wixproj`.

## Prerequisites

- .NET SDK 8.0
- WiX Toolset 5.0.1
- Visual Studio 2022 or later


## Detailed Explanation

### WebSocket Server (`SimpleWS-Server\Program.cs`)

The WebSocket server listens for incoming WebSocket connections and handles communication with connected clients.

### WebSocket Client (`ClientWorkerService\Worker.cs`)

The WebSocket client connects to the server and communicates using WebSockets. The client is set up in `ClientWorkerService\Program.cs`, where it reads configuration values and initializes the `Worker` service.

### Client Setup (`ClientWorkerService\Program.cs`)

The client application is configured to run as a Windows service. It uses Serilog for logging and reads configuration values from `appsettings.json` and environment variables.

### MSI Installer (`ClientInstaller\Package.wxs`)

The MSI installer is defined in `ClientInstaller\Package.wxs`. It includes custom actions to check for a `ClientId` and abort the installation if it is not provided.

### Custom Action (`RequireClientIdAction.wxs`)

The custom action defined in `RequireClientIdAction.wxs` checks for the presence of a `ClientId` and aborts the installation if it is not provided.

## Building and Running the Server Locally

1. **Build the Server**:
    
## Setting Up the WebSocket Server

1. **Navigate to the Server Directory**:
`cd SimpleWS-Server`

2. **Build and Run the Server**:
   `dotnet build`
   `dotnet run`

   
## Setting Up the WebSocket Client

1. **Navigate to the Client Directory**:
    `cd ClientWorkerService`
   
2. **Build and Publish the Client**:
    `dotnet publish -c Release -r win-x64 /p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml`
   
## Creating the MSI Installer

1. **Navigate to the Installer Directory**:
  `cd ClientInstaller`
  
2. **Build the MSI Installer**:
    `dotnet build`
   
## Running the MSI Installer on a Remote Server

1. **Transfer the MSI File to the Remote Server**:
    Use your preferred method to transfer the generated MSI file to the remote server.

2. **Run the MSI Installer**:
    `msiexec /i path\to\your\installer.msi ClientId=your-client-id`
   
    
