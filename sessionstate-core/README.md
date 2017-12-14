# Google Cloud Datastore Sample

A sample demonstrating how to use Google Cloud Datastore from ASP.NET sessions.

## Links

- [Cloud Datastore Docs](https://cloud.google.com/datastore/docs/)

## Build and Run

1.  **Follow the instructions in the [root README](../README.md)**.

4.  Enable APIs for your project.
    [Click here](https://console.cloud.google.com/flows/enableapi?apiid=datastore.googleapis.com&showconfirmation=true)
    to visit Cloud Platform Console and enable the Google Cloud Datastore API.

6.  Open [SessionState.sln](SessionStote.sln) with Microsoft Visual Studio version 2017 or later.

7.  In appsettings.json, replace `YOUR-PROJECT-ID` with your Google project id.

8.  Build and run the Solution.

## Measured performance.
On two n1-standard1 instances running in us-central-1f:

### In-Memory
Total elapsed seconds: 61.193
Average page fetch time in milliseconds: 7.3105

### Datastore



## Contributing changes

* See [CONTRIBUTING.md](../CONTRIBUTING.md)

## Licensing

* See [LICENSE](../LICENSE)
