# Cloud Storage Sample

A sample demonstrating how to invoke Google Cloud Datastore from C#.

## Links

- [Cloud Datastore Docs](https://cloud.google.com/datastore/docs/)

## Build and Run

1.  **Create a project in the Google Cloud Platform Console**.
    If you haven't already created a project, create one now. Projects enable
    you to manage all Google Cloud Platform resources for your app, including
    deployment, access control, billing, and services.
    1.  Open the [Cloud Platform Console](https://console.cloud.google.com/).
    2.  In the drop-down menu at the top, select Create a project.
    3.  Click Show advanced options. Under App Engine location, select a
        United States location.
    4.  Give your project a name.
    5.  Make a note of the project ID, which might be different from the project
        name. The project ID is used in commands and in configurations.

2.  **Enable billing for your project**.
    If you haven't already enabled billing for your project,
    [enable billing now](https://console.cloud.google.com/project/_/settings).
    Enabling billing allows the application to consume billable resources such
    as running instances and storing data.

3.  **Enable APIs for your project**.
    [Click here](https://console.cloud.google.com/flows/enableapi?apiid=datastore.googleapis.com&showconfirmation=true)
    to visit Cloud Platform Console and enable the Cloud Storage API.

4.  Download or clone this repo with

    ```sh
    git clone https://github.com/GoogleCloudPlatform/dotnet-docs-samples
    ```

5.  

6.  Open [Datastore.sln](Datastore.sln) with Microsoft Visual Studio version 2012 or later.

7.  Edit `QuickStart\Program.cs`, and replace YOUR-PROJECT-ID with id
    of the project you created in step 1.

8.  Build the Solution.

9.  From the command line, run QuickStart.exe to see a list of 
    subcommands:

    ```sh
    C:\...\bin\Debug> QuickStart.exe
    Usage:
      QuickStart create [new-bucket-name]
      QuickStart list
      QuickStart list bucket-name [prefix] [delimiter]
      QuickStart get-metadata bucket-name object-name
      QuickStart make-public bucket-name object-name
      QuickStart upload bucket-name local-file-path [object-name]
      QuickStart copy source-bucket-name source-object-name dest-bucket-name dest-object-name
      QuickStart move bucket-name source-object-name dest-object-name
      QuickStart download bucket-name object-name [local-file-path]
      QuickStart download-byte-range bucket-name object-name range-begin range-end [local-file-path]
      QuickStart print-acl bucket-name
      QuickStart print-acl bucket-name object-name
      QuickStart add-owner bucket-name user-email
      QuickStart add-owner bucket-name object-name user-email
      QuickStart add-default-owner bucket-name user-email
      QuickStart remove-owner bucket-name user-email
      QuickStart remove-owner bucket-name object-name user-email
      QuickStart remove-default-owner bucket-name user-email
      QuickStart delete bucket-name
      QuickStart delete bucket-name object-name [object-name] 
    ```

## Contributing changes

* See [CONTRIBUTING.md](../../CONTRIBUTING.md)

## Licensing

* See [LICENSE](../../LICENSE)
