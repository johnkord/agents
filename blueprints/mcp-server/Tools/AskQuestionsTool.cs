using ModelContextProtocol.Server;
using System.ComponentModel;

namespace McpServer.Tools;

[McpServerToolType]
public static class AskQuestionsTool
{
    [McpServerTool, Description("Asks the user one or more clarifying questions and waits for their response. Use when you need additional information to proceed with a task.")]
    public static string AskQuestions(
        [Description("The question or list of questions to ask the user.")] string questions)
    {
        throw new NotImplementedException();
    }
}
