using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RedirectMapTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Where is the base url (including trailing slash) e.g http://epiphanysearch.co.uk/ ?");
            string baseurl = Console.ReadLine();

            Console.WriteLine("Where is the rewrite map file located?");
            string path = Console.ReadLine();

            //path = "C:/test/rewritemaps.config";
            //baseurl = "http://www.slatergordon.co.uk/";

            DataTable table = ReadRewriteRulesXml(path);

            List<Result> results = TestUrls(table, baseurl);

            //var results = await TestUrlsTask(table, baseurl);

            CreateCSV(results, baseurl);

            //MainAsync(args).Wait();
        }

        static async Task MainAsync(string[] args)
        {
            Console.WriteLine("Where is the base url (including trailing slash) e.g http://epiphanysearch.co.uk/ ?");
            string baseurl = Console.ReadLine();

            Console.WriteLine("Where is the rewrite map file located?");
            string path = Console.ReadLine();

            DataTable table = ReadRewriteRulesXml(path);

            List<Result> results = TestUrls(table, baseurl);

            //var results = await TestUrlsTask(table, baseurl);

            CreateCSV(results, baseurl);
        }

        static DataTable ReadRewriteRulesXml(string path)
        {
            DataSet ds = new DataSet();
            ds.ReadXml(path);
            return ds.Tables["add"];
        }

        static async Task<List<Result>> TestUrlsTask(DataTable urls, string baseUrl)
        {           

            var taskList = new List<Task<Result>>();

            foreach (DataRow row in urls.Rows)
            {
                HttpClient client = new HttpClient() { MaxResponseContentBufferSize = 1000000 };
                // do something
                var result = new Result();

                result.RequestedUrl = (string)row["key"];
                result.ExpectedResponseUrl = (string)row["value"];

                if (!result.ExpectedResponseUrl.EndsWith("/"))
                {
                    result.ExpectedResponseUrl = result.ExpectedResponseUrl + "/";
                }

                var task = ProcessUrlAsync(result, baseUrl, client);
                task.ConfigureAwait(false);
                taskList.Add(task);
            }

            List<Result> results = new List<Result>();
            
            await Task.WhenAll(taskList).ContinueWith( (Task<Result[]> tl) =>
            {
                Result[] resultArray = tl.Result;
                results.AddRange(resultArray);
            });

            return results;
        }

        static async Task<Result> ProcessUrlAsync(Result testRequest, string baseUrl, HttpClient client)
        {
            try
            {
                var response = await client.GetAsync(baseUrl + testRequest.RequestedUrl);

                testRequest.ResponseCode = response.StatusCode;
                testRequest.ResponseUrl = response.RequestMessage.RequestUri.ToString();

                if (testRequest.ResponseUrl == baseUrl + testRequest.ExpectedResponseUrl)
                {
                    testRequest.Match = true;
                }
            }
            catch(Exception ex)
            {
                testRequest.ResponseCode = HttpStatusCode.BadRequest;
            }

            return testRequest;
        }

        static void CreateCSV(List<Result> results, string baseurl)
        {
            //before your loop
            var csv = new StringBuilder();

            var header = string.Format("{0},{1},{2},{3},{4}", "Url to redirect","Url to redirect to","Url redirected to","Http Code","Correct?");
            csv.AppendLine(header);

            foreach (var result in results)
            {
                var newLine = string.Format("{0},{1},{2},{3},{4}", baseurl + result.RequestedUrl, baseurl + result.ExpectedResponseUrl, result.ResponseUrl, result.ResponseCode.ToString(), result.Match.ToString());
                csv.AppendLine(newLine);

                //after your loop
            }

			//change this to the location you want to output the CSV to.
            File.WriteAllText("C:/test/test.csv", csv.ToString());

        }

        //None Async
        static Result GetUrlResult(Result testRequest, string baseUrl)
        {
            var html = string.Empty;
            var url = baseUrl + testRequest.RequestedUrl;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.AutomaticDecompression = DecompressionMethods.GZip;

            request.ServicePoint.Expect100Continue = false;
            request.ProtocolVersion = HttpVersion.Version11;

            request.Timeout = 5000;
            request.KeepAlive = true;

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                testRequest.ResponseCode = response.StatusCode;
                testRequest.ResponseUrl = response.ResponseUri.ToString();



                if (testRequest.ResponseUrl == baseUrl + testRequest.ExpectedResponseUrl.TrimStart('/'))
                {
                    testRequest.Match = true;
                }
            }
            catch (WebException ex)
            {
                testRequest.ResponseCode = HttpStatusCode.NotFound;
            }

            Console.WriteLine(testRequest.RequestedUrl + " " + testRequest.Match);

            return testRequest;

        }

        static List<Result> TestUrls(DataTable urls, string baseUrl)
        {
            List<Result> results = new List<Result>();

            foreach (DataRow row in urls.Rows)
            {
                var result = new Result();

                result.RequestedUrl = (string)row["key"];
                result.ExpectedResponseUrl = (string)row["value"];

                if (!result.ExpectedResponseUrl.EndsWith("/"))
                {
                    result.ExpectedResponseUrl = result.ExpectedResponseUrl + "/";
                }

                result = GetUrlResult(result, baseUrl);
                results.Add(result);
            }

            return results;
        }
       
    }


    class Result
    {
        public string RequestedUrl { get; set; }
        public string ExpectedResponseUrl { get; set; }
        public string ResponseUrl { get; set; }
        public HttpStatusCode ResponseCode { get; set; }
        public bool Match { get; set; }
    }
}
