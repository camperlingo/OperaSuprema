import re

with open("src/GUI/MainWindow.axaml.cs", "r", encoding="utf-8") as f:
    text = f.read()

def replacer(match):
    indent = match.group(1)
    statement = match.group(2)
    # se è già dentro un lock (non probabile se la regex è stretta) o se stiamo escludendo
    if "lock" in statement:
        return match.group(0)
    return f"{indent}lock (_historyLock) {{ {statement} }}"

# Rimpiazza le chiamate singole
for collection in ["_chatHistory", "_jakHistory", "_currentSession.Messages"]:
    for method in ["Add", "Clear", "AddRange", "RemoveAt"]:
        pattern = r'^([ \t]*)((' + re.escape(collection) + r'\.' + method + r'\(.*?\);))$'
        text = re.compile(pattern, re.MULTILINE).sub(replacer, text)

# Rimpiazza costruttori copia: var temp = new List...(_chatHistory);
text = re.compile(r'^([ \t]*)(var tempHistory = new List<Dictionary<string, string>>\(_chatHistory\);)$', re.MULTILINE).sub(
    lambda m: f"{m.group(1)}List<Dictionary<string, string>> tempHistory;\n{m.group(1)}lock (_historyLock) {{ tempHistory = new List<Dictionary<string, string>>(_chatHistory); }}", text)

text = re.compile(r'^([ \t]*)(var newHistory = new List<Dictionary<string, string>> { _chatHistory\[0\] };)$', re.MULTILINE).sub(
    lambda m: f"{m.group(1)}List<Dictionary<string, string>> newHistory;\n{m.group(1)}lock (_historyLock) {{ newHistory = new List<Dictionary<string, string>> {{ _chatHistory[0] }}; }}", text)

with open("src/GUI/MainWindow.axaml.cs", "w", encoding="utf-8") as f:
    f.write(text)
