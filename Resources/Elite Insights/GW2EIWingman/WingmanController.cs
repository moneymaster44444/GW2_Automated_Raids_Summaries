﻿using System.Net;
using System.Text;
using GW2EIWingman.WingmanUploadJsons;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;

[assembly: CLSCompliant(false)]
namespace GW2EIWingman;

/// <summary>
/// https://gw2wingman.nevermindcreations.de/api
/// </summary>
public static class WingmanController
{
    public delegate void TraceHandler(string trace);

    private static readonly JsonSerializerOptions DeserializerSettings = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IncludeFields = true,
    };
    private static readonly UTF8Encoding NoBOMEncodingUTF8 = new(false);
    private static readonly HttpClient HTTPClient = new HttpClient()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };


    ///////////////// URL Utilities
    private const string BaseURL = "https://gw2wingman.nevermindcreations.de/";
    private const string TestConnectionURL = BaseURL + "testConnection";

    private const string BaseAPIURL = BaseURL + "api/";
    private const string EIVersionURL = BaseAPIURL + "EIversion";
    private const string ImportLogQueuedURL = BaseAPIURL + "importLogQueued";
    private const string CheckLogQueuedURL = BaseAPIURL + "checkLogQueued";
    private const string CheckLogQueuedOrDBURL = BaseAPIURL + "checkLogQueuedOrDB";
    private const string CheckUploadURL = BaseURL + "checkUpload";
    private const string UploadProcessedURL = BaseURL + "uploadProcessed";

    private static bool IsDPSReportLinkValid(string dpsReportLink, TraceHandler traceHandler)
    {
        if (!dpsReportLink.Contains("https") && !dpsReportLink.Contains(".report"))
        {
            traceHandler("Invalid dps.report link");
            return false;
        }
        return true;
    }

    private static string GetImportLogQueuedURL(string dpsReportLink)
    {
        return ImportLogQueuedURL + "?link=" + dpsReportLink;
    }

    private static string GetCheckLogQueuedURL(string dpsReportLink)
    {
        return CheckLogQueuedURL + "?link=" + dpsReportLink;
    }

    private static string GetCheckLogQueuedOrDBURL(string dpsReportLink)
    {
        return CheckLogQueuedOrDBURL + "?link=" + dpsReportLink;
    }

    // Connection checking
    private static bool CheckConnection(TraceHandler traceHandler)
    {
        bool check = _GetWingmanResponse("CheckConnection", TestConnectionURL, traceHandler, HttpMethod.Get) == "True";
        traceHandler("connection " + (check ? "OK" : "KO"));
        return check;
    }

    private static bool VerifyEIVersion(Version parserVersion, TraceHandler traceHandler)
    {
        string? returnedVersion = _GetWingmanResponse("EIVersionURL", EIVersionURL, traceHandler, HttpMethod.Get);
        if (returnedVersion == null)
        {
            traceHandler("Could not fetch version from Wingman");
            return true;
        }
        returnedVersion = returnedVersion.Replace("v", "");
        var expectedVersion = new Version(returnedVersion);
        traceHandler("Used version " + parserVersion.ToString());
        bool check = parserVersion.CompareTo(expectedVersion) >= 0;
        traceHandler("Version " + (check ? "up to date" : "outdated"));
        if (!check)
        {
            traceHandler("Expected version to be at least " + expectedVersion.ToString() + ", please update your EI.");
        }
        return true;
    }

    public static bool CanBeUsed(Version? parserVersion, TraceHandler traceHandler)
    {
        return CheckConnection(traceHandler) && (parserVersion == null || VerifyEIVersion(parserVersion, traceHandler));
    }
    //

    public static WingmanCheckLogQueuedOrDBObject? GetCheckLogQueuedOrDB(string dpsReportLink, TraceHandler traceHandler)
    {
        if (!IsDPSReportLinkValid(dpsReportLink, traceHandler))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<WingmanCheckLogQueuedOrDBObject>(GetWingmanResponse("CheckLogQueuedOrDB", GetCheckLogQueuedOrDBURL(dpsReportLink), traceHandler, null, HttpMethod.Post)!, DeserializerSettings);
        }
        catch (Exception e)
        {
            traceHandler("CheckLogQueuedOrDB failed - " + e.Message);
            return null;
        }
    }

    public static WingmanCheckLogQueuedObject? GetCheckLogQueued(string dpsReportLink, TraceHandler traceHandler)
    {
        if (!IsDPSReportLinkValid(dpsReportLink, traceHandler))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<WingmanCheckLogQueuedObject>(GetWingmanResponse("CheckLogQueued", GetCheckLogQueuedOrDBURL(dpsReportLink), traceHandler, null, HttpMethod.Post)!, DeserializerSettings);
        }
        catch (Exception e)
        {
            traceHandler("CheckLogQueued failed - " + e.Message);
            return null;
        }
    }

    public static WingmanImportLogQueuedObject? ImportLogQueued(string dpsReportLink, TraceHandler traceHandler)
    {
        if (!IsDPSReportLinkValid(dpsReportLink, traceHandler))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<WingmanImportLogQueuedObject>(GetWingmanResponse("ImportLogQueued", GetCheckLogQueuedOrDBURL(dpsReportLink), traceHandler, null, HttpMethod.Post)!, DeserializerSettings);
        }
        catch (Exception e)
        {
            traceHandler("ImportLogQueued failed - " + e.Message);
            return null;
        }
    }

    public static bool UploadToWingmanUsingImportLogQueued(string dpsReportLink, TraceHandler traceHandler)
    {
        // Check if the URL is already present on Wingman
        WingmanCheckLogQueuedOrDBObject? wingmanCheck = GetCheckLogQueuedOrDB(dpsReportLink, traceHandler);
        if (wingmanCheck != null)
        {
            if (wingmanCheck.InDB.GetValueOrDefault(false) || wingmanCheck.InQueue.GetValueOrDefault(false))
            {
                traceHandler("Upload failed - Log already present in Wingman DB");
                return false;
            }
            else
            {
                WingmanImportLogQueuedObject? wingmanUpload = ImportLogQueued(dpsReportLink, traceHandler);
                if (wingmanUpload != null)
                {
                    if (wingmanUpload.Success != 1)
                    {
                        traceHandler("Upload failed - " + wingmanUpload.Note);
                        return true;
                    }
                    else
                    {
                        traceHandler("Upload successful - " + wingmanUpload.Note);
                        return true;
                    }
                }
                return false;
            }
        }
        return false;
    }

    public static bool CheckUploadPossible(FileInfo fi, string account, long triggerID, TraceHandler traceHandler)
    {
        string creationTime = new DateTimeOffset(fi.CreationTime).ToUnixTimeSeconds().ToString();
        var data = new Dictionary<string, string> {
            { "account", account },
            { "filesize", fi.Length.ToString() },
            { "timestamp", creationTime },
            { "file", fi.Name } ,
            { "triggerID", triggerID.ToString() }
        };
        HttpContent contentCreator()
        {
            var multiPartContent = new MultipartFormDataContent();
            foreach (KeyValuePair<string, string> pair in data)
            {
                var content = new StringContent(pair.Value, NoBOMEncodingUTF8, "text/plain");
                multiPartContent.Add(content, pair.Key);
            }
            return multiPartContent;
        }
        return GetWingmanResponse("CheckUploadPossible", CheckUploadURL, traceHandler, null, HttpMethod.Post, contentCreator) == "True";
    }
    public static bool UploadProcessed(FileInfo fi, string account, byte[] jsonFile, byte[] htmlFile, string suffix, TraceHandler traceHandler, Version parserVersion)
    {
        //var data = new Dictionary<string, string> { { "account", account }, { "file", File.ReadAllText(fi.FullName) }, { "jsonfile", jsonString }, { "htmlfile", htmlString } };
        byte[] fileBytes = File.ReadAllBytes(fi.FullName);
        string name = fi.Name;
        string jsonName = Path.GetFileNameWithoutExtension(fi.Name) + suffix + ".json";
        string htmlName = Path.GetFileNameWithoutExtension(fi.Name) + suffix + ".html";
        string jsonString = NoBOMEncodingUTF8.GetString(jsonFile);
        string htmlString = NoBOMEncodingUTF8.GetString(htmlFile);
        var data = new Dictionary<string, string> {
            { "account", account },
        };
        Func<HttpContent> contentCreator = () =>
        {
            var multiPartContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.Add("Content-Type", "application/octet-stream");
            multiPartContent.Add(fileContent, "file", name);
            var jsonContent = new StringContent(jsonString, NoBOMEncodingUTF8, "text/plain");
            multiPartContent.Add(jsonContent, "jsonfile", jsonName);
            var htmlContent = new StringContent(htmlString, NoBOMEncodingUTF8, "text/plain");
            multiPartContent.Add(htmlContent, "htmlfile", htmlName);
            foreach (KeyValuePair<string, string> pair in data)
            {
                var content = new StringContent(pair.Value, NoBOMEncodingUTF8, "text/plain");
                multiPartContent.Add(content, pair.Key);
            }
            return multiPartContent;
        };

        string? response = GetWingmanResponse("UploadProcessed", UploadProcessedURL, traceHandler, null, HttpMethod.Post, contentCreator);
        return response != null && response != "False";
    }

    //
    private static string? _GetWingmanResponse(string requestName, string url, TraceHandler traceHandler, HttpMethod method, Func<HttpContent>? content = null)
    {
        const int tentatives = 3;
        for (int i = 0; i < tentatives; i++)
        {
            traceHandler(requestName + " tentative");
            var webService = new Uri(@url);
            using var requestMessage = new HttpRequestMessage(method, webService);
            requestMessage.Headers.ExpectContinue = false;

            if (content != null)
            {
                requestMessage.Content = content();
            }

            try
            {
                Task<HttpResponseMessage> httpRequest = HTTPClient.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead);
                HttpResponseMessage httpResponse = httpRequest.Result;
                HttpStatusCode statusCode = httpResponse.StatusCode;
                HttpContent responseContent = httpResponse.Content;

                if (statusCode != HttpStatusCode.OK)
                {
                    throw new HttpRequestException(statusCode.ToString());
                }

                if (responseContent != null)
                {
                    Task<string> stringContentsTask = responseContent.ReadAsStringAsync();
                    string stringContents = stringContentsTask.Result;
                    traceHandler(requestName + " successful: " + stringContents);
                    return stringContents;
                }
            }
            catch (AggregateException agg)
            {
                traceHandler(requestName + " tentative failed");
                traceHandler("Main reason: " + agg.Message);         
                foreach (Exception e in agg.InnerExceptions)
                {
                    traceHandler(requestName + " tentaive failed - sub message - " + e.Message);
                }
            }
            catch (Exception e)
            {
                traceHandler(requestName + " tentaive failed");
                traceHandler("Reason: " + e.Message);
            }
        }
        return null;
    }


    private static string? GetWingmanResponse(string requestName, string url, TraceHandler traceHandler, Version? parserVersion, HttpMethod method, Func<HttpContent>? content = null)
    {
        if (!CanBeUsed(parserVersion, traceHandler))
        {
            return default;
        }
        return _GetWingmanResponse(requestName, url, traceHandler, method, content);
    }

}
