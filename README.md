# What is it?

This library simplifies the code to do Merge Replication from a SQL Server Compact 3.5 SP2 client, with useful helper methods

Features:

- Is intended for use from a WinForms or WPF application, and the Synchronize method runs async.
- Implements best practices for optimal performance, and athttp://www.nuget.org/packages/ErikEJ.SqlCeMergeLib/)tempt to properly detect expired subscriptions, by throwing a PublicationMayHaveExpiredException. 
- Will create the database file for you as required, so an existing database file is not required.
- Optionally logs sync status to a SyncLog table (which is a part of the publication)
- Generate INSERT script in order to rescue local data in case of a disaster (for example publication expiry)
- Validate a Publication, for example after initial Sync
- Properly format a SqlCeException as a string to get all available error information
- Source includes a demo form to test parameters and see the library in action
- Exposes the settings described in the [Rob Tiffany "cheat sheet"](http://robtiffany.com/mobile-merge-replication-performance-and-scalability-cheat-sheet/)

# How do I get it?

Download the [NuGet package](http://www.nuget.org/packages/ErikEJ.SqlCeMergeLib/)

# How do I use it?

[Online API documentation](https://erikej.github.io/SqlCeMergeLib/)

[Sample WinForms app](https://github.com/ErikEJ/SqlCeMergeLib/blob/master/docs/Sample.zip)

```csharp
using ErikEJ.SqlCeMergeLib;
using System.Data.SqlServerCe;
...
string sdfFile = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MergeTest.sdf");
conn = new SqlCeConnection(string.Format("Data Source={0}", sdfFile));

DateTime syncDate = sync.GetLastSuccessfulSyncTime(conn);
textBox1.Text = "Last Sync: " + syncDate.ToString();

sync.Completed += SyncCompletedEvent;
sync.Progress += SyncProgressEvent;
sync.Synchronize(conn, 1002, 1);
```

Other useful methods:

Generate INSERT script for the local database (for disaster recovery):
```csharp
public string GenerateInsertScripts (
        SqlCeConnection connection,
        List<string> tableNames
) 
```

Format a SqlCeException as a String:
```csharp
public string ShowErrors (
        SqlCeException e
) 
```

Validate that the local database is properly Merge Replicated;
```csharp
public bool Validate (
        SqlCeConnection connection
) 
```

Configuration:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="InternetLogin" value=""/>
    <add key="InternetPassword" value=""/>
    <add key="InternetUrl" value="http://erik-pc/ssce35sync/sqlcesa35.dll"/>
    <add key="Publication" value="PubPostCodes"/>
    <add key="Publisher" value="Erik-PC\SQL2008R2"/>
    <add key="PublisherDatabase" value="PostCodes"/>
    <add key="PublisherLogin" value="sa"/>
    <add key="PublisherPassword" value="pw"/>
    <add key="UseNT" value="false"/>
  </appSettings>
</configuration>
```
![Screenshot](https://github.com/ErikEJ/SqlCeMergeLib/blob/master/img/repl.jpg)

