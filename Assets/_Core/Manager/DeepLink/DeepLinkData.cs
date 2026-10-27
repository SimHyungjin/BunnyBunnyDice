using System;
using System.Collections.Generic;

/// <summary>
/// 딥링크 하나를 해석한 결과.
///
/// <c>mygame://shop/item?id=5&amp;from=push</c> 를 받으면
/// Host는 "shop", Path는 "/item", Query는 id=5, from=push가 된다.
/// </summary>
public sealed class DeepLinkData
{
    /// <summary>원본 주소.</summary>
    public string Url { get; }

    /// <summary>첫 구간. 어디로 보낼지 가르는 기준이 된다.</summary>
    public string Host { get; }

    /// <summary>나머지 경로.</summary>
    public string Path { get; }

    private readonly Dictionary<string, string> _query = new();

    /// <summary>쿼리 값. 없으면 기본값.</summary>
    public string Get(string key, string defaultValue = null)
        => !string.IsNullOrEmpty(key) && _query.TryGetValue(key, out string value) ? value : defaultValue;

    public bool Has(string key) => !string.IsNullOrEmpty(key) && _query.ContainsKey(key);

    private DeepLinkData(string url, string host, string path)
    {
        Url = url;
        Host = host;
        Path = path;
    }

    /// <summary>주소를 해석한다. 형식이 맞지 않으면 null.</summary>
    public static DeepLinkData Parse(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        try
        {
            var uri = new Uri(url);
            var data = new DeepLinkData(url, uri.Host, uri.AbsolutePath);
            data.ParseQuery(uri.Query);
            return data;
        }
        catch (Exception e)
        {
            // 외부에서 들어오는 값이라 어떤 형태든 올 수 있다. 던지지 않고 무시한다.
            Debug.LogWarning($"[DeepLink] 주소를 해석하지 못했습니다: {url} ({e.Message})");
            return null;
        }
    }

    private void ParseQuery(string query)
    {
        if (string.IsNullOrEmpty(query)) return;

        // 앞의 '?'를 떼고 & 로 나눈다.
        string body = query[0] == '?' ? query.Substring(1) : query;
        string[] pairs = body.Split('&');

        for (int i = 0; i < pairs.Length; i++)
        {
            string pair = pairs[i];
            if (string.IsNullOrEmpty(pair)) continue;

            int separator = pair.IndexOf('=');
            if (separator < 0)
            {
                _query[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            string key = Uri.UnescapeDataString(pair.Substring(0, separator));
            string value = Uri.UnescapeDataString(pair.Substring(separator + 1));

            if (!string.IsNullOrEmpty(key)) _query[key] = value;
        }
    }
}
