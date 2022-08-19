using System;
using Azure;
using Azure.AI.Translation.Document;

using Azure.AI.Language.QuestionAnswering;

using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Azure.AI.TextAnalytics;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using Azure.Core;
using Microsoft.Extensions.Configuration;

namespace AI_Labb1
{

    public class Config
    {
        public ConsoleColor botColor { get; private set; }
        public ConsoleColor userColor { get; private set; }


        public Uri translateEndpoint { get; private set; }
        public Uri questionsEndpoint { get; private set; }
        public string translatekey { get; private set; }
        public string questionsKey { get; private set; }
        public string location { get; private set; }
        public AzureKeyCredential questionsCreds { get; private set; }
        public string projectName { get; private set; }
        public string deploymentName { get; private set; }

        public Config()
        {
            botColor = ConsoleColor.Blue;
            userColor = ConsoleColor.White;

            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("config.json");
            IConfigurationRoot conf = builder.Build();

            translateEndpoint = new Uri(conf["translateEndpoint"]);
            translatekey = conf["translateKey"];

            questionsEndpoint = new Uri(conf["questionsEndpoint"]);
            questionsKey = conf["questionsKey"];

            projectName = conf["projectName"];
            deploymentName = conf["deploymentName"];

            location = conf["location"];

            questionsCreds = new AzureKeyCredential(questionsKey);
        }
    }

    public class BotChat
    {
        private Config config = new Config();


        private string ParseInput(string question)
        {
            string parsedQuestion = question;
            
            if(question.Contains("today"))
            {
                parsedQuestion = question.Replace("today", DateTime.Now.ToString("dddd"));
            }
            else if(question.Contains("tomorrow"))
            {
                parsedQuestion = question.Replace("tomorrow", DateTime.Now.AddDays(1).ToString("dddd"));
            }
            return parsedQuestion;
        }

        

        private async Task<string> AskBot(string translatedQuestion)
        {
            try
            {
                QuestionAnsweringClient client = new QuestionAnsweringClient(config.questionsEndpoint, config.questionsCreds);
                QuestionAnsweringProject project = new QuestionAnsweringProject(config.projectName, config.deploymentName);

                Response<AnswersResult> response = await client.GetAnswersAsync(translatedQuestion, project);
                if(response.Value.Answers[0].Confidence <= 0.7)
                {
                    return "I did not understand your question. Please ask another one.";
                }
                return response.Value.Answers[0].Answer;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return null;
        }

        private async Task<string> TranslateText(string question)
        {
            string route = $"translate?api-version=3.0&to=en";
            object[] body = new object[] { new { Text = question } };
            var requestBody = JsonConvert.SerializeObject(body);

            var client = new HttpClient();
            var request = new HttpRequestMessage();

            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(config.translateEndpoint + route);
            request.Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", config.translatekey);
            request.Headers.Add("Ocp-Apim-Subscription-Region", config.location);

            HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            string data = await response.Content.ReadAsStringAsync();

            JArray jsonResponse = JArray.Parse(data);
            return (string)jsonResponse[0]["translations"][0]["text"];
        }

        private Task<string> BotInput(string question)
        {
            Task<string> translatedQuestion = TranslateText(question);
            return AskBot(translatedQuestion.Result);
        }

        public void BotRun()
        {
            bool done = false;
            Console.ForegroundColor = config.botColor;
            Console.WriteLine("Welcome to the chat bot of Company. Ask me anything!");
            while(!done)
            {
                Console.ForegroundColor = config.userColor;
                string question = Console.ReadLine();
                question = ParseInput(question);
                Console.ForegroundColor = config.botColor;
                var answer = BotInput(question);
                Console.WriteLine(answer.Result);
                Console.WriteLine("Do you want to ask something else?");
            }
        }

    }

    class Program
    {
        public static int Main(string[] args)
        {
            BotChat bot = new BotChat();
            bot.BotRun();
            return 0;
        }
    }
}
