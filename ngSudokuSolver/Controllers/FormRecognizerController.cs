using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ngSudokuSolver.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ngSudokuSolver.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class FormRecognizerController : ControllerBase
    {
        static string endpoint;
        static string apiKey;

        public FormRecognizerController()
        {
            endpoint = "FormRecognizer API endpoint";
            apiKey = "FormRecognizer API key";
        }

        [HttpPost, DisableRequestSizeLimit]
        public async Task<string[][]> Post()
        {
            try
            {
                string[][] sudokuArray = GetNewSudokuArray();

                if (Request.Form.Files.Count > 0)
                {
                    var file = Request.Form.Files[Request.Form.Files.Count - 1];

                    if (file.Length > 0)
                    {
                        var memoryStream = new MemoryStream();
                        file.CopyTo(memoryStream);
                        byte[] imageFileBytes = memoryStream.ToArray();
                        memoryStream.Flush();

                        string SudokuLayoutJSON = await GetSudokuBoardLayout(imageFileBytes);
                        if (SudokuLayoutJSON.Length > 0)
                        {
                            sudokuArray = GetSudokuBoardItems(SudokuLayoutJSON);
                        }
                    }
                }

                return sudokuArray;
            }
            catch(Exception e)
            {
                throw;
            }
        }

        static async Task<string> GetSudokuBoardLayout(byte[] byteData)
        {
            HttpClient client = new();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            //ben: warn since the migration ...
            // https://github.com/MicrosoftDocs/azure-docs/blob/main/articles/applied-ai-services/form-recognizer/v3-migration-guide.md
            // POST request :   https://{your-form-recognizer-endpoint}/formrecognizer/documentModels/{modelId}?api-version=2022-01-30-preview
            // GET REQUEST  :   https://{your-form-recognizer-endpoint}/formrecognizer/documentModels/{modelId}/AnalyzeResult/{resultId}?api-version=2022-01-30-preview
            //---
            string uri = endpoint + "formrecognizer/v2.1-preview.3/layout/analyze";  ///layout/analyze => /documentModels/prebuilt-layout:analyze?api-version=2022-01-30-preview
            uri = endpoint + "/formrecognizer/documentModels/prebuilt-layout:analyze?api-version=2022-01-30-preview";

            string LayoutJSON = string.Empty;

            using (ByteArrayContent content = new(byteData))
            {
                HttpResponseMessage response;
                content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                response = await client.PostAsync(uri, content);

                if (response.IsSuccessStatusCode)
                {
                    HttpHeaders headers = response.Headers;

                    if (headers.TryGetValues("Operation-Location", out IEnumerable<string> values))
                    {
                        string OperationLocation = values.First();
                        LayoutJSON = await GetJSON(OperationLocation);
                    }
                }
            }
            return LayoutJSON;
        }

        static async Task<string> GetJSON(string endpoint)
        {
            using var client = new HttpClient(new HttpRetryMessageHandler(new HttpClientHandler()));
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri(endpoint);

            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            var response = await client.SendAsync(request);
            var result = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return result;
        }

        static string[][] GetSudokuBoardItems(string LayoutData)
        {
            string[][] sudokuArray = GetNewSudokuArray();
            dynamic array = JsonConvert.DeserializeObject(LayoutData);
            //int countOfCells = ((JArray)array?.analyzeResult?.pageResults[0]?.tables[0]?.cells).Count;
            int countOfCells = ((JArray)array?.analyzeResult?.tables[0]?.cells).Count;



            for (int i = 0; i < countOfCells; i++)
            {
                //int rowIndex = array.analyzeResult.pageResults[0].tables[0].cells[i].rowIndex;
                //int columnIndex = array.analyzeResult.pageResults[0].tables[0].cells[i].columnIndex;
                //sudokuArray[rowIndex][columnIndex] = array.analyzeResult.pageResults[0].tables[0].cells[i]?.text;

                int rowIndex = array.analyzeResult.tables[0].cells[i].rowIndex;
                int columnIndex = array.analyzeResult.tables[0].cells[i].columnIndex;

                var content = array.analyzeResult.tables[0].cells[i]?.content.ToString();
                //clean content wehre some time is :
                // "content": "7 :selected:",
                // "content": ":unselected:",
                // "content": "8 :unselected: :unselected:",
                var ok = content.Split(" ")[0];
                ok = ok.Replace(":unselected:", "");
                ok = ok.Replace(":selected:", "");
                ok = ok.Replace(" ", "");
                sudokuArray[rowIndex][columnIndex] = ok;
            }
            return sudokuArray;
        }

        static string[][] GetNewSudokuArray()
        {
            string[][] sudokuArray = new string[9][];

            for (int i = 0; i < 9; i++)
            {
                sudokuArray[i] = new string[9];
            }

            return sudokuArray;
        }
    }
}
