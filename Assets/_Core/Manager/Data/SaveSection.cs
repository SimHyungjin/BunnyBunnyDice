using System.IO;

/// <summary>
/// 저장 단위. 섹션 하나가 파일 하나이고, 클라우드 동기화도 섹션 단위로 한다.
/// 코어와 게임이 각자 필요한 섹션을 정의한다 — 코어 파일을 고쳐 게임 필드를 넣지 않는다.
///
/// <b>JSON 섹션(기본)</b>: public 필드만 선언하면 된다. 필드 단위로 저장되며(프로퍼티는 [JsonProperty]를 붙인 것만),
/// Dictionary·List·중첩 클래스를 쓸 수 있다. 저장 파일에 없는 필드는 선언의 기본값으로 남으므로
/// 필드를 추가하는 것만으로는 버전을 올릴 필요가 없다.
///
/// <b>바이너리 섹션</b>: <see cref="IBinarySection"/>을 구현하면 그 섹션은 바이트로 직접 읽고 쓴다.
/// 크거나 자주 저장되는 데이터(스테이지 기록 등)를 비트 단위로 묶어 크기를 줄일 때 쓴다.
///
/// 인스턴스는 <see cref="DataManager.Get{T}"/>가 만들고 계속 같은 것을 돌려준다(초기화·가져오기도 내용만 바꾼다).
/// 값을 바꾼 뒤에는 <see cref="MarkDirty"/>를 부른다.
///
/// 관리 코드 스트리핑을 High로 올리면 리플렉션으로만 쓰이는 필드가 제거될 수 있다. 그 경우 섹션 클래스에 [Preserve]를 붙인다.
/// </summary>
public abstract class SaveSection
{
    /// <summary>
    /// 파일 이름이자 클라우드 키. 기본은 타입 이름이다.
    /// 클래스 이름을 바꾸면 기존 세이브와 연결이 끊기므로, 출시 후에도 이름이 바뀔 수 있다면 고정 문자열로 override한다.
    /// 파일 이름으로 쓸 수 있는 문자만 쓴다.
    /// </summary>
    public virtual string Key => GetType().Name;

    /// <summary>
    /// 현재 스키마 버전. 필드 의미가 바뀌거나 이름을 바꿔 변환이 필요할 때만 올리고 <see cref="Migrate"/>에서 옮긴다.
    /// </summary>
    public virtual int Version => 1;

    /// <summary>저장된 버전이 <see cref="Version"/>보다 낮을 때, 로드 직후 불린다. 변환 뒤에는 자동으로 다시 저장된다.</summary>
    protected internal virtual void Migrate(int fromVersion) { }

    internal bool IsDirty;

    /// <summary>값이 바뀌었음을 알린다. 다음 저장 시점(백그라운드 전환·씬 전환·자동 저장)에 이 섹션만 다시 쓴다.</summary>
    public void MarkDirty() => IsDirty = true;
}

/// <summary>
/// 섹션을 바이트로 직접 읽고 쓴다. 쓰는 순서와 읽는 순서가 정확히 같아야 한다.
/// 필드를 추가하면 <see cref="SaveSection.Version"/>을 올리고, <see cref="Read"/>에서 version을 보고 갈라 읽는다.
/// </summary>
public interface IBinarySection
{
    void Write(BinaryWriter writer);

    /// <param name="version">파일에 저장될 당시의 버전.</param>
    void Read(BinaryReader reader, int version);
}
