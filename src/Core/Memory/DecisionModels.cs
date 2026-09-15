using System;
using System.Collections.Generic;

namespace OperaSuprema.Core.Memory;

public enum DecisionStatus
{
    Active,
    Superseded,
    Deprecated,
    Rejected
}

public record DecisionRecord(
    string DecisionId,
    string EntityId,
    DecisionStatus Status,
    string? SupersededById,
    string Subject,
    string Predicate,
    string ObjectValue,
    string? Rationale,
    double Confidence,
    DateTime CreatedAt
);

public record ExtractedTriple(
    string Subject,
    string Predicate,
    string ObjectValue,
    string? Rationale = null,
    double Confidence = 1.0
);

public record RetrievalQuery(
    string RawText,
    IReadOnlyList<string> ExtractedKeywords,
    string ChatId,
    int TokenBudget = 2048,
    bool IncludeSuperseded = false
);

public record ContextPayload(
    IReadOnlyList<DecisionRecord> ActiveDecisions,
    IReadOnlyList<string> RetrievedSnippets,
    string DeterministicPrefixBlock,
    int EstimatedTokens
);
