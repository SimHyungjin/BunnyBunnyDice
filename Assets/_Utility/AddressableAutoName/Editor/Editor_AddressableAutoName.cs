using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

/// <summary>
/// 에셋을 Addressable로 등록하면 주소를 전체 경로 대신 에셋 이름으로 바꾼다. (중복이면 _1, _2 …)
/// </summary>
[InitializeOnLoad]
public static class Editor_AddressableAutoName
{
    static Editor_AddressableAutoName()
    {
        // 설정 인스턴스(AddressableAssetSettingsDefaultObject.Settings)의 이벤트가 아니라 정적 이벤트를 구독한다.
        // 인스턴스 이벤트는 로드 시점에 설정이 없으면 구독할 수 없는데, 설정은 처음 Addressable 체크박스를
        // 누르는 순간 만들어지므로 그 첫 등록부터 이름이 바뀌지 않는다.
        AddressableAssetSettings.OnModificationGlobal -= OnModification;
        AddressableAssetSettings.OnModificationGlobal += OnModification;
    }

    private static void OnModification(AddressableAssetSettings settings, AddressableAssetSettings.ModificationEvent ev, object data)
    {
        if (ev != AddressableAssetSettings.ModificationEvent.EntryAdded) return;

        // 인스펙터 체크박스는 List로, Groups 창 드래그 등 단일 등록은 항목 하나로 넘어온다.
        switch (data)
        {
            case List<AddressableAssetEntry> entries:
                foreach (var entry in entries) Rename(settings, entry);
                break;
            case AddressableAssetEntry entry:
                Rename(settings, entry);
                break;
        }
    }

    private static void Rename(AddressableAssetSettings settings, AddressableAssetEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.AssetPath)) return;

        string assetName = Path.GetFileNameWithoutExtension(entry.AssetPath);

        var usedAddresses = new HashSet<string>();
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            foreach (var other in group.entries)
                if (other != entry) usedAddresses.Add(other.address);
        }

        string newAddress = assetName;
        for (int counter = 1; usedAddresses.Contains(newAddress); counter++)
            newAddress = $"{assetName}_{counter}";

        entry.address = newAddress;
    }
}
