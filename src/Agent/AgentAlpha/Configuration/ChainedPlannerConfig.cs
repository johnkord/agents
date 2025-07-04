namespace AgentAlpha.Configuration;

/// <summary>
/// Fine-tuning knobs for the ChainedPlanner.
/// </summary>
public class ChainedPlannerConfig
{
    public string AnalyseModel { get; set; }  = "gpt-4.1-nano";
    public string OutlineModel { get; set; }  = "gpt-4.1-nano";
    public string DetailModel  { get; set; }  = "gpt-4.1";
    public int MaxTokens       { get; set; }  = 2048;
}
