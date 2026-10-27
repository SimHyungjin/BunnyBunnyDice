using System;

/// <summary>
/// 화면에 보이는 글을 담은 string 필드(또는 string 배열·리스트)에 붙인다. 번역표 갱신 툴이 이 필드의 글을 모아 번역표 원문으로 넣는다.
/// 표시는 값을 그대로 쓰지 않고 <see cref="App.Localize"/>를 거친다 — 필드에는 원문(한국어)이 그대로 남고 번역은 표에만 있다.
///
/// PropertyAttribute가 아닌 이유: [TextArea]처럼 그리는 방식을 정하는 속성과 함께 붙여야 하는데,
/// PropertyAttribute는 필드 하나에 그리개 하나만 적용돼 둘 중 하나가 무시된다.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class LocalizedAttribute : Attribute
{
}
