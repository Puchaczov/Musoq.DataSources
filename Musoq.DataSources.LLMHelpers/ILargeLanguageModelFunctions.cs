// ReSharper disable InconsistentNaming
namespace Musoq.DataSources.LLMHelpers;

public interface ILargeLanguageModelFunctions<in TEntity>
{
    bool IsContentAbout(TEntity entity, string content, string question);

    string Sentiment(TEntity entity, string content, bool throwOnUnknown = false);

    string SummarizeContent(TEntity entity, string content);
    
    string TranslateContent(TEntity entity, string content, string from, string to);

    string[] Entities(TEntity entity, string content, bool throwOnException = false);

    string LlmPerform<TColumnType>(TEntity entity, string whatToDo, TColumnType column);

    string DescribeImage(TEntity entity, string imageBase64);

    string AskImage(TEntity entity, string question, string imageBase64);

    bool IsQuestionApplicableToImage(TEntity entity, string question, string imageBase64);

    int CountTokens(TEntity entity, string content);

    int CountTokens(string content);
}