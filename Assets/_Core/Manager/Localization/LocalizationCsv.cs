using System.Collections.Generic;
using System.Text;

/// <summary>
/// 번역표 CSV 읽기·쓰기(RFC 4180). 구글 시트·엑셀이 내보내는 형식 그대로 읽어야 해서
/// 따옴표 안의 쉼표·줄바꿈·"" 이스케이프를 처리한다. 줄바꿈은 \n 하나로 맞춘다(시트가 \r\n으로 내보내도 원문과 같은 키가 되게).
/// </summary>
public static class LocalizationCsv
{
    public static List<string[]> Parse(string text)
    {
        List<string[]> rows = new List<string[]>();
        if (string.IsNullOrEmpty(text)) return rows;

        List<string> row = new List<string>();
        StringBuilder cell = new StringBuilder();
        bool quoted = false;
        // BOM은 엑셀이 붙인다. 남기면 첫 열 이름이 "﻿ko"가 되어 원문 열을 찾지 못한다.
        int i = text[0] == '﻿' ? 1 : 0;

        while (i < text.Length)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else quoted = false;
                }
                else if (c == '\r')
                {
                    // 칸 안의 \r\n은 \n 하나로.
                    if (i + 1 >= text.Length || text[i + 1] != '\n') cell.Append('\n');
                }
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRow(rows, row, cell);
                row = new List<string>();
            }
            else cell.Append(c);
            i++;
        }
        EndRow(rows, row, cell);
        return rows;
    }

    private static void EndRow(List<string[]> rows, List<string> row, StringBuilder cell)
    {
        row.Add(cell.ToString());
        cell.Clear();
        // 빈 줄(파일 끝 줄바꿈 등)은 버린다.
        if (row.Count == 1 && row[0].Length == 0) return;
        rows.Add(row.ToArray());
    }

    public static string Write(List<string[]> rows)
    {
        StringBuilder builder = new StringBuilder();
        for (int r = 0; r < rows.Count; r++)
        {
            string[] row = rows[r];
            for (int c = 0; c < row.Length; c++)
            {
                if (c > 0) builder.Append(',');
                AppendCell(builder, row[c] ?? string.Empty);
            }
            builder.Append('\n');
        }
        return builder.ToString();
    }

    // 필요할 때만 따옴표로 감싼다 — 전부 감싸면 비교(diff)할 때 읽기 어렵다.
    private static void AppendCell(StringBuilder builder, string value)
    {
        bool quote = value.Length > 0 && (value[0] == ' ' || value[value.Length - 1] == ' ');
        for (int i = 0; i < value.Length && !quote; i++)
        {
            char c = value[i];
            if (c == ',' || c == '"' || c == '\n' || c == '\r') quote = true;
        }
        if (!quote)
        {
            builder.Append(value);
            return;
        }
        builder.Append('"');
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '"') builder.Append('"');
            builder.Append(value[i]);
        }
        builder.Append('"');
    }
}
