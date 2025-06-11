using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ThinkAndJobSolution.Controllers.MainHome.Prl
{
    [Route("api/v1/PRL")]
    [ApiController]
    [Authorize]
    public class PrlFormController : ControllerBase
    {
        //Ayuda

        public struct Question
        {
            public string type { get; set; }
            public string question { get; set; }
            public bool answer { get; set; } //Used for truefalse
            public bool candidateAnswer { get; set; } //Used for truefalse (OPCIONAL)
            public List<Answer> answers { get; set; } //Used for select and selectmulti
        }

        public struct Answer
        {
            public string text { get; set; }
            public bool correct { get; set; }
            public bool candidateCorrect { get; set; } // (OPTIONAL)
        }
        public static List<Question> parseQuestions(JsonElement json)
        {
            List<Question> questions = new();
            if (json.ValueKind != JsonValueKind.Array) return questions;

            foreach (JsonElement itemJson in json.EnumerateArray())
            {
                if (itemJson.TryGetProperty("type", out JsonElement typeJson) && itemJson.TryGetProperty("question", out JsonElement questionJson))
                {
                    Question question = new Question()
                    {
                        type = typeJson.GetString(),
                        question = questionJson.GetString()
                    };

                    switch (question.type)
                    {
                        case "truefalse":
                            if (itemJson.TryGetProperty("answer", out JsonElement answerJson))
                            {
                                question.answer = answerJson.GetBoolean();
                            }
                            else continue;
                            if (itemJson.TryGetProperty("candidateAnswer", out JsonElement candidateAnswerJson))
                                question.candidateAnswer = candidateAnswerJson.GetBoolean();
                            break;
                        case "select":
                        case "selectmulti":
                            if (itemJson.TryGetProperty("answers", out JsonElement answersJson) && answersJson.ValueKind == JsonValueKind.Array)
                            {
                                question.answers = new();
                                foreach (JsonElement answareJson in answersJson.EnumerateArray())
                                {
                                    if (answareJson.TryGetProperty("text", out JsonElement textJson) && answareJson.TryGetProperty("correct", out JsonElement correctJson))
                                    {
                                        Answer answare = new Answer()
                                        {
                                            text = textJson.GetString(),
                                            correct = correctJson.GetBoolean()
                                        };
                                        if (itemJson.TryGetProperty("candidateCorrect", out JsonElement candidateCorrectJson))
                                            answare.candidateCorrect = candidateCorrectJson.GetBoolean();
                                        question.answers.Add(answare);
                                    }
                                }
                            }
                            else continue;
                            break;
                        default:
                            continue;
                    }

                    questions.Add(question);
                }
            }

            return questions;
        }
        public static List<Question> parseQuestions(string jsonString)
        {
            return parseQuestions(JsonDocument.Parse(jsonString).RootElement);
        }
    }
}
