using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace Nytherion.Editor
{
    /// <summary>
    /// ScriptableObject 데이터와 CSV 파일 간의 동기화를 담당하는 유틸리티
    /// </summary>
    public static class DataSyncUtility
    {
        /// <summary>
        /// 데이터를 CSV 파일로 저장
        /// </summary>
        public static void ExportToCSV(string filePath, string[] headers, List<string[]> rows)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));

            foreach (string[] row in rows)
            {
                // CSV 특수문자(쉼표 등) 처리
                for (int i = 0; i < row.Length; i++)
                {
                    if (row[i].Contains(",") || row[i].Contains("\"") || row[i].Contains("\n"))
                    {
                        row[i] = "\"" + row[i].Replace("\"", "\"\"") + "\"";
                    }
                }
                sb.AppendLine(string.Join(",", row));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[DataSync] Exported to: {filePath}");
        }

        /// <summary>
        /// CSV 파일을 읽어서 데이터 리스트로 반환
        /// </summary>
        public static List<Dictionary<string, string>> ImportFromCSV(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length < 2) return null;

            string[] headers = ParseCSVLine(lines[0]);
            List<Dictionary<string, string>> data = new List<Dictionary<string, string>>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] values = ParseCSVLine(lines[i]);
                Dictionary<string, string> entry = new Dictionary<string, string>();

                for (int j = 0; j < headers.Length; j++)
                {
                    if (j < values.Length) entry[headers[j]] = values[j];
                }
                data.Add(entry);
            }

            return data;
        }

        private static string[] ParseCSVLine(string line)
        {
            List<string> values = new List<string>();
            bool inQuotes = false;
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            values.Add(sb.ToString());
            return values.ToArray();
        }
    }
}
