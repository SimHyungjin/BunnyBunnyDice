using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

/// <summary>값이 SO 필드가 아니라 계산으로 나오는 토큰(영웅 공격력 비례 피해 등)을 가진 카드가 구현한다.</summary>
public interface ICardTextTokens
{
    /// <param name="hero">지금 영웅 능력치. 모르는 곳이면 null — 그때는 배율처럼 영웅 없이 읽히는 글을 낸다.</param>
    bool TryGetToken(string key, HeroStats hero, out string text);
}

/// <summary>
/// 카드 글의 {토큰}을 그 카드 SO의 값으로 채운다. 글에 숫자를 적어 두면 SO 수치를 고칠 때마다 글도 고쳐야 하고,
/// 놓치면 카드가 실제와 다른 말을 한다(포켓 "10%"가 실제 20%였다). 그래서 글에는 이름만 적고 숫자는 여기서 넣는다.
/// - {Duration}       필드 값 (예: 4, 0.4)
/// - {Ratio%}         필드 값 × 100 + % (예: 40%)
/// - {1+Bonus}        1 + 필드 값 — "n배"로 읽히는 증가 비율 (예: Bonus 1 → 2)
/// - {1-Falloff%}     1 − 필드 값 — 줄어드는 몫 (예: Falloff 0.75 → 25%)
/// - 그 밖의 이름은 ICardTextTokens가 먼저 받는다(영웅 능력치 비례 값 등).
/// 필드는 리플렉션으로 찾는다 — 카드마다 글 채우는 코드를 따로 두지 않게. 모르는 토큰은 그대로 남기고 경고한다(오타가 화면에 보이게).
/// 글은 먼저 지금 언어로 바꾸고(App.Localize) 토큰을 채운다 — 번역도 같은 토큰을 쓰므로 숫자는 언어와 상관없이 SO에서 온다.
/// </summary>
public static class CardText
{
    private static readonly Dictionary<(Type, string), FieldInfo> Fields = new Dictionary<(Type, string), FieldInfo>();
    private static readonly StringBuilder Builder = new StringBuilder();

    public static string Fill(string template, object source, HeroStats hero = null)
    {
        template = App.Localize(template);
        if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0 || source == null) return template;

        Builder.Clear();
        int i = 0;
        while (i < template.Length)
        {
            int open = template.IndexOf('{', i);
            int close = open < 0 ? -1 : template.IndexOf('}', open + 1);
            if (close < 0)
            {
                Builder.Append(template, i, template.Length - i);
                break;
            }
            Builder.Append(template, i, open - i);
            string token = template.Substring(open + 1, close - open - 1);
            if (TryResolve(token, source, hero, out string text)) Builder.Append(text);
            else
            {
                Builder.Append(template, open, close - open + 1);
                Debug.LogWarning($"[CardText] {source}: 모르는 토큰 {{{token}}}");
            }
            i = close + 1;
        }
        return Builder.ToString();
    }

    private static bool TryResolve(string token, object source, HeroStats hero, out string text)
    {
        if (source is ICardTextTokens custom && custom.TryGetToken(token, hero, out text)) return true;

        text = null;
        string name = token;
        bool percent = name.EndsWith("%", StringComparison.Ordinal);
        if (percent) name = name.Substring(0, name.Length - 1);
        float sign = 0f;
        if (name.StartsWith("1+", StringComparison.Ordinal)) sign = 1f;
        else if (name.StartsWith("1-", StringComparison.Ordinal)) sign = -1f;
        if (sign != 0f) name = name.Substring(2);

        if (!TryGetNumber(source, name, out float value)) return false;
        if (sign != 0f) value = 1f + sign * value;
        text = percent ? FormatPercent(value) : value.ToString("0.##", CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>비율을 "%" 글로. 소수 둘째 자리까지 — 최대 체력 피해처럼 1%보다 작은 값(0.05%)이 한 자리로는 반올림돼 틀리게 보였다.</summary>
    public static string FormatPercent(float ratio) => (ratio * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static bool TryGetNumber(object source, string name, out float value)
    {
        value = 0f;
        Type type = source.GetType();
        if (!Fields.TryGetValue((type, name), out FieldInfo field))
        {
            field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Fields[(type, name)] = field;
        }
        if (field == null) return false;
        object raw = field.GetValue(source);
        if (raw is float f) value = f;
        else if (raw is int n) value = n;
        else return false;
        return true;
    }
}
