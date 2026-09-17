import re
import sys

with open("src/GUI/MainWindow.axaml.cs", "r", encoding="utf-8") as f:
    lines = f.readlines()

new_lines = []
skip = 0

for i, line in enumerate(lines):
    if skip > 0:
        skip -= 1
        continue
        
    stripped = line.strip()
    
    # 1. Initialize _historyLock
    if "_chatHistory = new();" in stripped:
        new_lines.append("        private readonly object _historyLock = new();\n")
        new_lines.append(line)
        continue

    # 2. Add
    if stripped.startswith("_chatHistory.Add(") or stripped.startswith("_jakHistory.Add(") or stripped.startswith("_currentSession.Messages.Add("):
        indent = line[:len(line) - len(line.lstrip())]
        new_lines.append(f"{indent}lock (_historyLock) {{ {stripped} }}\n")
        continue

    # 3. Clear
    if stripped.startswith("_chatHistory.Clear()") or stripped.startswith("_jakHistory.Clear()") or stripped.startswith("_currentSession.Messages.Clear()"):
        indent = line[:len(line) - len(line.lstrip())]
        new_lines.append(f"{indent}lock (_historyLock) {{ {stripped} }}\n")
        continue

    # 4. AddRange
    if stripped.startswith("_chatHistory.AddRange(") or stripped.startswith("_jakHistory.AddRange(") or stripped.startswith("_currentSession.Messages.AddRange("):
        indent = line[:len(line) - len(line.lstrip())]
        new_lines.append(f"{indent}lock (_historyLock) {{ {stripped} }}\n")
        continue

    # 5. RemoveAt
    if stripped.startswith("_chatHistory.RemoveAt(") or stripped.startswith("_jakHistory.RemoveAt(") or stripped.startswith("_currentSession.Messages.RemoveAt("):
        indent = line[:len(line) - len(line.lstrip())]
        new_lines.append(f"{indent}lock (_historyLock) {{ {stripped} }}\n")
        continue

    # 6. Copying / Snapshot / Reading
    if "var tempHistory = new List<Dictionary<string, string>>(_chatHistory);" in stripped:
        indent = line[:len(line) - len(line.lstrip())]
        new_lines.append(f"{indent}List<Dictionary<string, string>> tempHistory;\n")
        new_lines.append(f"{indent}lock (_historyLock) {{ tempHistory = new List<Dictionary<string, string>>(_chatHistory); }}\n")
        continue
        
    if "var newHistory = new List<Dictionary<string, string>> { _chatHistory[0] };" in stripped:
        indent = line[:len(line) - len(line.lstrip())]
        new_lines.append(f"{indent}List<Dictionary<string, string>> newHistory;\n")
        new_lines.append(f"{indent}lock (_historyLock) {{ newHistory = new List<Dictionary<string, string>> {{ _chatHistory[0] }}; }}\n")
        continue

    # 7. Check System Prompt and Replace
    if stripped == 'if (_chatHistory.Count > 0 && _chatHistory[0]["role"] == "system")':
        if i + 2 < len(lines) and '_chatHistory[0]["content"] =' in lines[i+2].strip():
            # Multiline if
            indent = line[:len(line) - len(line.lstrip())]
            new_lines.append(f"{indent}lock (_historyLock) {{\n")
            new_lines.append(line)
            new_lines.append(lines[i+1])
            new_lines.append(lines[i+2])
            new_lines.append(lines[i+3])
            new_lines.append(f"{indent}}}\n")
            skip = 3
            continue
        elif i + 1 < len(lines) and '_chatHistory[0]["content"] =' in lines[i+1].strip():
            # One line if without braces
            indent = line[:len(line) - len(line.lstrip())]
            new_lines.append(f"{indent}lock (_historyLock) {{\n")
            new_lines.append(line)
            new_lines.append(lines[i+1])
            new_lines.append(f"{indent}}}\n")
            skip = 1
            continue
            
    # 8. Check Count for Pruning
    if stripped == 'if (_currentSession.Messages.Count + 1 < _chatHistory.Count)':
        if i + 5 < len(lines) and '_chatHistory.Clear();' in lines[i+4].strip():
            indent = line[:len(line) - len(line.lstrip())]
            new_lines.append(f"{indent}lock (_historyLock) {{\n")
            new_lines.append(line)
            new_lines.append(lines[i+1])
            new_lines.append(lines[i+2])
            new_lines.append(lines[i+3])
            new_lines.append(lines[i+4])
            new_lines.append(lines[i+5])
            new_lines.append(f"{indent}}}\n")
            skip = 5
            continue
            
    # 9. Pop logic
    if stripped == 'if (_chatHistory.Count > 0 && _chatHistory.Last()["role"] == "user")':
        if i + 1 < len(lines) and '_chatHistory.RemoveAt' in lines[i+1]:
            indent = line[:len(line) - len(line.lstrip())]
            new_lines.append(f"{indent}lock (_historyLock) {{\n")
            new_lines.append(line)
            new_lines.append(lines[i+1])
            new_lines.append(f"{indent}}}\n")
            skip = 1
            continue

    if stripped == 'if (_chatHistory.Count > 0 && _chatHistory.Last()["role"] == "assistant")':
        if i + 1 < len(lines) and '_chatHistory.Last()["content"] +=' in lines[i+1]:
            indent = line[:len(line) - len(line.lstrip())]
            new_lines.append(f"{indent}lock (_historyLock) {{\n")
            new_lines.append(line)
            new_lines.append(lines[i+1])
            new_lines.append(f"{indent}}}\n")
            skip = 1
            continue
            
    # 10. if (_chatHistory != null && _chatHistory.Count > 0) with LastOrDefault
    if stripped == 'if (_chatHistory != null && _chatHistory.Count > 0)':
        if i + 1 < len(lines) and '_chatHistory.LastOrDefault' in lines[i+1]:
            indent = line[:len(line) - len(line.lstrip())]
            new_lines.append(f"{indent}lock (_historyLock) {{\n")
            new_lines.append(line)
            new_lines.append(lines[i+1])
            new_lines.append(f"{indent}}}\n")
            skip = 1
            continue

    new_lines.append(line)

with open("src/GUI/MainWindow.axaml.cs", "w", encoding="utf-8") as f:
    f.writelines(new_lines)
