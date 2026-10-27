using System.Collections.Generic;
using UnityEngine;

public enum PackageType
{
    UPM,             
    OpenUPM,         
    UnityPackage,    
}

public enum FavoriteRegistryType
{
    UnityRegistry,
    MyAssets,
}

[System.Serializable]
public class PackageEntry
{
    public string     name;
    public string     category;
    public PackageType type;
    [Tooltip("UPM: Git URL / OpenUPM: com.xxx.xxx / UnityPackage: https://...unitypackage")]
    public string     url;
    public string     description;
    [Tooltip("UPM의 경우 비워두면 최신, #v1.2.3 형태로 입력 가능")]
    public string     version;
}

[System.Serializable]
public class FavoriteItem
{
    public string name = "";
    [Tooltip("패키지 ID (예: com.unity.addressables). UnityRegistry는 설치에 필수, MyAssets는 UPM 형태로 설치되는 경우 감지용")]
    public string id = "";
    public FavoriteRegistryType registryType = FavoriteRegistryType.UnityRegistry;
    public string description = "";
}

public class SO_PackageData : ScriptableObject
{
    public List<PackageEntry> packages = new List<PackageEntry>();
    public List<FavoriteItem> favorites = new List<FavoriteItem>();
}