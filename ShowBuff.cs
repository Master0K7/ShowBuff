using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ImGuiNET;
using SharpDX;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace ShowBuff_exileapi;

public class ShowBuff : BaseSettingsPlugin<ShowBuffSettings>
{
    private string T(string en, string ru) => Settings.UseEnglish ? en : ru;
    private List<Buff> _detectedBuffs = new List<Buff>();
    private HashSet<string> _lastKnownActiveBuffNames = new HashSet<string>();
    private string _buffSearchFilter = "";
    private int _buffSortMode = 0; // 0 = по имени, 1 = по стакам, 2 = по типу
    
    // Известные дебаффы и ground эффекты (lowercase)
    private static readonly HashSet<string> KnownDebuffsAndGroundEffects = new HashSet<string>
    {
        // Проклятия (Curses)
        "vulnerability", "elemental_weakness", "temporal_chains", "enfeeble", "despair",
        "frostbite", "flammability", "conductivity", "punishment", "warlords_mark",
        "poachers_mark", "assassins_mark", "sniper_mark",
        
        // Метки и состояния (Marks & Ailments)
        "bleed", "poison", "ignite", "freeze", "chill", "shock", "corrupted_blood",
        "burning", "bleeding", "poisoned", "shocked", "chilled", "frozen",
        
        // Дебаффы (Debuffs)
        "hindered", "maim", "impale", "brittle", "sapped", "scorch", "slow", "stun",
        "taunt", "blind", "intimidate", "unnerve", "exposure", "withered",
        
        // Ground эффекты - вредные (Harmful Ground Effects)
        "ground_desecration", "desecration", "ground_caustic", "caustic_ground",
        "ground_burning", "burning_ground", "ground_chilled", "chilled_ground",
        "ground_shocked", "shocked_ground", "ground_tar", "tar", "ground_ice",
        "ground_fire", "ground_lightning", "ground_chaos", "ground_poison",
        "ground_bleed", "ground_corrupted", "ground_volatile", "volatile_ground",
        
        // Дополнительные ground эффекты
        "ground_effect", "ground_degen", "degen", "damage_over_time", "dot"
    };
    
    // Полезные ground эффекты (Beneficial Ground Effects)
    private static readonly HashSet<string> BeneficialGroundEffects = new HashSet<string>
    {
        "consecrated_ground", "consecrated", "ground_consecrated",
        "blessed_ground", "blessed", "ground_blessed",
        "ground_regen", "regeneration_ground", "ground_healing"
    };

    public override bool Initialise()
    {
        Name = "ShowBuff";
        UpdateDetectedBuffs();
        return true;
    }

    public override void DrawSettings()
    {
        base.DrawSettings();

        ImGui.Separator();
        ImGui.Text(T("Buff Settings", "Настройки баффов"));

        ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1),
            T("Examples: unique_nearby_allies_are_lucky, player_aura_resists, ground_desecration",
              "Примеры: unique_nearby_allies_are_lucky, player_aura_resists, ground_desecration"));
        ImGui.TextColored(new System.Numerics.Vector4(1, 0.6f, 0.2f, 1),
            T("Auras: set Min Stacks = 0 and enable 'Hide Count' if no stacks",
              "Ауры: ставьте Min Stacks = 0 и включайте 'Hide Count' если стаков нет"));
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1),
            T("Find names in DevTree -> Player -> Buffs",
              "Имена берите в DevTree -> Player -> Buffs"));

        for (int i = 0; i < Settings.BuffSettings.Count; i++)
        {
            var buff = Settings.BuffSettings[i];
            ImGui.PushID(i);

            bool show = buff.Show.Value;
            if (ImGui.Checkbox($"##Show{i}", ref show))
                buff.Show.Value = show;
            ImGui.SameLine();

            var buffName = buff.BuffName.Value;
            ImGui.SetNextItemWidth(150);
            if (ImGui.InputText(T("Buff Name", "Название баффа") + $"##{i}", ref buffName, 100))
                buff.BuffName.Value = buffName;
            ImGui.SameLine();

            var display = buff.DisplayName.Value;
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputText(T("Display", "Отображение") + $"##{i}", ref display, 64))
                buff.DisplayName.Value = display;
            ImGui.SameLine();

            if (ImGui.Button($"-##{i}"))
            {
                Settings.BuffSettings.RemoveAt(i);
                ImGui.PopID();
                i--;
                continue;
            }

            if (show)
            {
                ImGui.Indent();

                var c = buff.TextColor.Value;
                var vec = new System.Numerics.Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
                if (ImGui.ColorEdit4(T("Color", "Цвет") + $"##{i}", ref vec))
                {
                    buff.TextColor.Value = new Color(
                        (int)(vec.X * 255), (int)(vec.Y * 255), (int)(vec.Z * 255), (int)(vec.W * 255));
                }

                int minStacks = buff.MinStacks.Value;
                if (ImGui.SliderInt(T("Min Stacks", "Мин. стаков") + $"##{i}", ref minStacks, 0, 100))
                    buff.MinStacks.Value = minStacks;

                bool hideStacks = buff.HideStackCount.Value;
                if (ImGui.Checkbox(T("Hide Count (auras with 0)", "Скрыть количество (ауры с 0)") + $"##{i}", ref hideStacks))
                    buff.HideStackCount.Value = hideStacks;

                bool useHead = buff.UseHeadPosition.Value;
                if (ImGui.Checkbox(T("Above Head", "Над головой") + $"##{i}", ref useHead))
                    buff.UseHeadPosition.Value = useHead;

                int x = buff.PositionX.Value;
                int y = buff.PositionY.Value;
                if (!useHead)
                {
                    if (ImGui.SliderInt(T("Position X", "Позиция X") + $"##{i}", ref x, -1500, 1500))
                        buff.PositionX.Value = x;
                    if (ImGui.SliderInt(T("Position Y", "Позиция Y") + $"##{i}", ref y, -1500, 1500))
                        buff.PositionY.Value = y;
                }
                else
                {
                    if (ImGui.SliderInt(T("Offset X", "Смещение X") + $"##{i}", ref x, -1500, 1500))
                        buff.PositionX.Value = x;
                    if (ImGui.SliderInt(T("Offset Y", "Смещение Y") + $"##{i}", ref y, -1500, 1500))
                        buff.PositionY.Value = y;
                }

                ImGui.Unindent();
            }

            ImGui.PopID();
        }

        if (ImGui.Button("+ " + T("Add Buff", "Добавить бафф")))
            Settings.BuffSettings.Add(new ShowBuffSetting { DisplayName = new ExileCore.Shared.Nodes.TextNode($"Buff{Settings.BuffSettings.Count + 1}") });

        ImGui.Separator();
        ImGui.Text(T("Detected Buffs", "Обнаруженные баффы"));
        ImGui.SameLine();
        if (ImGui.Button(T("Refresh", "Обновить") + "##RefreshDetectedBuffs"))
        {
            UpdateDetectedBuffs();
        }
        ImGui.SameLine();
        bool showAllBuffsWindow = Settings.ShowAllBuffsWindow.Value;
        if (ImGui.Checkbox(T("Show All Buffs Window", "Показать окно всех баффов") + "##ShowAllBuffsWindowCheckbox", ref showAllBuffsWindow))
        {
            Settings.ShowAllBuffsWindow.Value = showAllBuffsWindow;
        }
        ImGui.SameLine();
        bool freezeBuffList = Settings.FreezeBuffList.Value;
        if (ImGui.Checkbox(T("Freeze List", "Заморозить список") + "##FreezeBuffListCheckbox", ref freezeBuffList))
        {
            Settings.FreezeBuffList.Value = freezeBuffList;
        }
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1),
            T("Active buffs on your character. Click + to add to configuration.",
              "Активные баффы на вашем персонаже. Нажмите + чтобы добавить в конфигурацию."));
        
        // Индикация заморозки
        if (Settings.FreezeBuffList.Value)
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.6f, 0, 1),
                T("⚠ List is FROZEN - updates stopped", "⚠ Список ЗАМОРОЖЕН - обновления остановлены"));
        }

        if (GameController.Player != null && GameController.Player.IsValid)
        {
            foreach (var buff in _detectedBuffs)
            {
                if (buff?.Name == null) continue;

                ImGui.Text($"{buff.Name} (");
                ImGui.SameLine();
                ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), buff.DisplayName);
                ImGui.SameLine();
                ImGui.Text($") (Stacks: {buff.BuffStacks})");
                ImGui.SameLine();
                
                // Обычная кнопка "+"
                if (ImGui.Button($"+##DetectedBuff_{buff.Name}"))
                {
                    if (!Settings.BuffSettings.Any(x => x.BuffName.Value == buff.Name))
                    {
                        Settings.BuffSettings.Add(new ShowBuffSetting
                        {
                            BuffName = new ExileCore.Shared.Nodes.TextNode(buff.Name),
                            DisplayName = new ExileCore.Shared.Nodes.TextNode(buff.DisplayName),
                            Show = new ExileCore.Shared.Nodes.ToggleNode(true)
                        });
                    }
                }
                
                // Индикация дебаффа
                if (IsLikelyDebuff(buff))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0.3f, 0.3f, 1), 
                        T("[DEBUFF]", "[ДЕБАФФ]"));
                }
                // Индикация полезного ground эффекта
                else if (IsBeneficialGroundEffect(buff))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new System.Numerics.Vector4(0.3f, 1, 0.3f, 1), 
                        T("[BENEFICIAL]", "[ПОЛЕЗНЫЙ]"));
                }
            }
        }
    }

    public override void Render()
    {
        if (!Settings.Enable)
            return;

        if (!Settings.ShowInHideout && GameController?.Area?.CurrentArea?.IsHideout == true)
            return;

        var player = GameController.Player;
        if (player == null || !player.IsValid)
            return;

        var currentActiveBuffNames = GetAllActiveBuffs(player)
            .Where(b => b?.Name != null)
            .Select(b => b.Name)
            .ToHashSet();

        // Обновляем список баффов только если не заморожен
        if (!Settings.FreezeBuffList.Value)
        {
            if (!_lastKnownActiveBuffNames.SetEquals(currentActiveBuffNames))
            {
                UpdateDetectedBuffs();
                _lastKnownActiveBuffNames = currentActiveBuffNames;
            }
        }

        var playerPos = player.Pos;
        if (playerPos.Equals(default(Vector3)))
            return;

        var screenPosSharp = GameController.IngameState.Camera.WorldToScreen(playerPos);
        if (screenPosSharp.Equals(default(SharpDX.Vector2)))
            return;

        var screenPos = new Vector2(screenPosSharp.X, screenPosSharp.Y);
        screenPos.Y -= Settings.HeightOffset;

        var active = GetActiveBuffs(player);
        DrawBuffs(screenPos, active);
        
        // Отдельное окно со всеми баффами (независимо от основного худа)
        if (Settings.ShowAllBuffsWindow.Value)
        {
            DrawAllBuffsWindow();
        }
    }

    private List<(string displayName, int count, Color color, bool useHeadPos, int posX, int posY, bool hideCount)> GetActiveBuffs(Entity player)
    {
        var result = new List<(string, int, Color, bool, int, int, bool)>();

        try
        {
            if (!player.TryGetComponent<Buffs>(out var buffComp))
                return result;

            var buffs = buffComp.BuffsList ?? new List<Buff>();

            foreach (var cfg in Settings.BuffSettings)
            {
                if (!cfg.Show.Value || string.IsNullOrWhiteSpace(cfg.BuffName.Value))
                    continue;

                int cnt = CountBuffsByName(buffs, cfg.BuffName.Value);
                if (cnt > cfg.MinStacks.Value)
                {
                    result.Add((
                        cfg.DisplayName.Value,
                        cnt,
                        cfg.TextColor.Value,
                        cfg.UseHeadPosition.Value,
                        cfg.PositionX.Value,
                        cfg.PositionY.Value,
                        cfg.HideStackCount.Value
                    ));
                }
            }
        }
        catch (Exception e)
        {
            LogError($"GetActiveBuffs error: {e.Message}", 5);
        }

        return result;
    }

    private static int CountBuffsByName(IEnumerable<Buff> buffs, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0;
        var needle = name.ToLowerInvariant();
        int count = 0;
        foreach (var b in buffs)
        {
            var n = b?.Name?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(n) && n.Contains(needle))
                count++;
        }
        return count;
    }

    private static bool IsLikelyDebuff(Buff buff)
    {
        if (buff?.Name == null) return false;
        
        var nameLower = buff.Name.ToLowerInvariant();
        
        // Проверяем по известным именам дебаффов и ground эффектов
        foreach (var debuffName in KnownDebuffsAndGroundEffects)
        {
            if (nameLower.Contains(debuffName))
                return true;
        }
        
        // Дополнительные эвристики
        if (nameLower.Contains("curse") || nameLower.Contains("debuff") || 
            nameLower.Contains("afflict") || nameLower.Contains("weaken") ||
            nameLower.Contains("ground_") || nameLower.Contains("_ground"))
            return true;
            
        return false;
    }

    private static bool IsBeneficialGroundEffect(Buff buff)
    {
        if (buff?.Name == null) return false;
        
        var nameLower = buff.Name.ToLowerInvariant();
        
        // Проверяем по известным полезным ground эффектам
        foreach (var beneficialName in BeneficialGroundEffects)
        {
            if (nameLower.Contains(beneficialName))
                return true;
        }
        
        return false;
    }

    private List<Buff> GetAllActiveBuffs(Entity player)
    {
        var result = new List<Buff>();
        try
        {
            if (player.TryGetComponent<Buffs>(out var buffComp))
            {
                var allBuffs = buffComp.BuffsList ?? new List<Buff>();
                
                // Возвращаем все баффы без фильтрации
                foreach (var buff in allBuffs)
                {
                    if (buff?.Name == null || buff.BuffDefinition == null) continue;
                    result.Add(buff);
                }
            }
        }
        catch (Exception e)
        {
            LogError($"GetAllActiveBuffs error: {e.Message}", 5);
        }
        return result;
    }

    private void UpdateDetectedBuffs()
    {
        var player = GameController.Player;
        if (player == null || !player.IsValid) return;

        // Получаем ВСЕ баффы без фильтрации по типам
        var currentActiveBuffs = new List<Buff>();
        try
        {
            if (player.TryGetComponent<Buffs>(out var buffComp))
            {
                var allBuffs = buffComp.BuffsList ?? new List<Buff>();
                currentActiveBuffs = allBuffs
                    .Where(b => b?.Name != null && b.BuffDefinition != null)
                    .GroupBy(b => b.Name)
                    .Select(g => g.First())
                    .ToList();
            }
        }
        catch (Exception e)
        {
            LogError($"UpdateDetectedBuffs error: {e.Message}", 5);
        }

        var filteredDetectedBuffs = _detectedBuffs
            .Where(existingBuff => currentActiveBuffs.Any(activeBuff => activeBuff.Name == existingBuff.Name))
            .ToList();

        var newlyActiveBuffs = currentActiveBuffs
            .Where(activeBuff => !filteredDetectedBuffs.Any(existingBuff => existingBuff.Name == activeBuff.Name))
            .ToList();

        var combinedBuffs = newlyActiveBuffs.Concat(filteredDetectedBuffs).ToList();

        // Убираем лимит в 5 баффов - показываем все
        _detectedBuffs = combinedBuffs
            .GroupBy(b => b.Name)
            .Select(g => g.First())
            .ToList();
    }

    private void DrawBuffs(Vector2 headPos, List<(string displayName, int count, Color color, bool useHeadPos, int posX, int posY, bool hideCount)> items)
    {
        if (items.Count == 0) return;

        using (Graphics.SetTextScale(Settings.FontSize))
        {
            float lineHeight = 20f;
            float currentY = headPos.Y;

            var sorted = items.OrderBy(i => i.useHeadPos ? 0 : 1).ToList();
            foreach (var (display, count, color, useHead, x, y, hide) in sorted)
            {
                var text = hide || count == 0 ? display : $"{display}: {count}";

                Vector2 pos = useHead
                    ? new Vector2(headPos.X + x, currentY + y)
                    : new Vector2(x, y);

                var size = Graphics.MeasureText(text);
                var textPos = new Vector2(pos.X - size.X / 2, pos.Y - size.Y / 2);

                if (Settings.ShowBackground)
                {
                    var bgPos = new Vector2(textPos.X - 5, textPos.Y - 2);
                    var bgSize = new Vector2(size.X + 10, size.Y + 4);
                    Graphics.DrawBox(bgPos, bgPos + bgSize, Settings.BackgroundColor);
                }

                Graphics.DrawText(text, textPos, color);

                if (useHead)
                    currentY += lineHeight * Settings.FontSize;
            }
        }
    }

    private void DrawAllBuffsWindow()
    {
        var isOpen = Settings.ShowAllBuffsWindow.Value;
        if (!ImGui.Begin(T("All Detected Buffs", "Все обнаруженные баффы"), ref isOpen))
        {
            Settings.ShowAllBuffsWindow.Value = isOpen;
            ImGui.End();
            return;
        }
        Settings.ShowAllBuffsWindow.Value = isOpen;

        // Заголовок и кнопка обновления
        ImGui.Text(T("All active buffs on your character", "Все активные баффы на вашем персонаже"));
        ImGui.SameLine();
        if (ImGui.Button(T("Refresh", "Обновить") + "##RefreshAllBuffs"))
        {
            UpdateDetectedBuffs();
        }
        ImGui.SameLine();
        bool freezeBuffList = Settings.FreezeBuffList.Value;
        if (ImGui.Checkbox(T("Freeze List", "Заморозить список") + "##FreezeBuffListAllBuffs", ref freezeBuffList))
        {
            Settings.FreezeBuffList.Value = freezeBuffList;
        }
        
        ImGui.Separator();
        
        // Поиск
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##BuffSearch", T("Search...", "Поиск..."), ref _buffSearchFilter, 100);
        ImGui.SameLine();
        
        // Сортировка
        ImGui.SetNextItemWidth(150);
        if (ImGui.Combo("##SortMode", ref _buffSortMode, 
            T("Sort: Name\0Sort: Stacks\0Sort: Type\0", "Сортировка: Имя\0Сортировка: Стаки\0Сортировка: Тип\0")))
        {
            // Сортировка изменена
        }
        
        ImGui.Separator();
        
        // Подсказки
        ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1),
            T("Type values shown for each buff. Use for Buff Type Filter in settings.",
              "Значения Type показаны для каждого баффа. Используйте для Buff Type Filter в настройках."));
        ImGui.TextColored(new System.Numerics.Vector4(1, 0.3f, 0.3f, 1),
            T("[DEBUFF] = harmful effect/ground", "[ДЕБАФФ] = вредный эффект/ground"));
        ImGui.SameLine();
        ImGui.TextColored(new System.Numerics.Vector4(0.3f, 1, 0.3f, 1),
            T("[BENEFICIAL] = helpful ground", "[ПОЛЕЗНЫЙ] = полезный ground"));
        
        // Индикация заморозки
        if (Settings.FreezeBuffList.Value)
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.6f, 0, 1),
                T("⚠ List is FROZEN - updates stopped", "⚠ Список ЗАМОРОЖЕН - обновления остановлены"));
        }
        
        ImGui.Separator();

        if (GameController.Player != null && GameController.Player.IsValid)
        {
            // Фильтрация и сортировка
            var filteredBuffs = _detectedBuffs.AsEnumerable();
            
            // Применяем поиск
            if (!string.IsNullOrWhiteSpace(_buffSearchFilter))
            {
                var searchLower = _buffSearchFilter.ToLowerInvariant();
                filteredBuffs = filteredBuffs.Where(b => 
                    b.Name.ToLowerInvariant().Contains(searchLower) || 
                    b.DisplayName.ToLowerInvariant().Contains(searchLower));
            }
            
            // Применяем сортировку
            filteredBuffs = _buffSortMode switch
            {
                1 => filteredBuffs.OrderByDescending(b => b.BuffStacks), // По стакам
                2 => filteredBuffs.OrderBy(b => b.BuffDefinition?.Type ?? 0), // По типу
                _ => filteredBuffs.OrderBy(b => b.Name) // По имени (default)
            };
            
            var buffList = filteredBuffs.ToList();
            
            ImGui.Text($"{T("Total", "Всего")}: {buffList.Count}");
            ImGui.Separator();
            
            ImGui.BeginChild("BuffsList", new Vector2(0, 0));
            
            foreach (var buff in buffList)
            {
                if (buff?.Name == null) continue;

                ImGui.PushID(buff.Name);
                
                // Информация о баффе
                ImGui.Text($"{buff.Name}");
                ImGui.SameLine();
                ImGui.TextColored(new System.Numerics.Vector4(0, 1, 0, 1), $"({buff.DisplayName})");
                ImGui.SameLine();
                ImGui.TextColored(new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1), $"Stacks: {buff.BuffStacks}");
                ImGui.SameLine();
                ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 1f, 1), $"Type: {buff.BuffDefinition?.Type ?? 0}");
                ImGui.SameLine();
                
                // Увеличенная кнопка "+" в 3 раза
                var buttonSize = new Vector2(ImGui.GetFontSize() * 3, ImGui.GetFontSize() * 3);
                if (ImGui.Button("+", buttonSize))
                {
                    if (!Settings.BuffSettings.Any(x => x.BuffName.Value == buff.Name))
                    {
                        Settings.BuffSettings.Add(new ShowBuffSetting
                        {
                            BuffName = new ExileCore.Shared.Nodes.TextNode(buff.Name),
                            DisplayName = new ExileCore.Shared.Nodes.TextNode(buff.DisplayName),
                            Show = new ExileCore.Shared.Nodes.ToggleNode(true)
                        });
                    }
                }
                
                // Индикация дебаффа
                if (IsLikelyDebuff(buff))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new System.Numerics.Vector4(1, 0.3f, 0.3f, 1), 
                        T("[DEBUFF]", "[ДЕБАФФ]"));
                }
                // Индикация полезного ground эффекта
                else if (IsBeneficialGroundEffect(buff))
                {
                    ImGui.SameLine();
                    ImGui.TextColored(new System.Numerics.Vector4(0.3f, 1, 0.3f, 1), 
                        T("[BENEFICIAL]", "[ПОЛЕЗНЫЙ]"));
                }
                
                ImGui.PopID();
                ImGui.Separator();
            }
            
            ImGui.EndChild();
        }
        else
        {
            ImGui.TextColored(new System.Numerics.Vector4(1, 0, 0, 1), 
                T("Player not found or invalid", "Игрок не найден или недоступен"));
        }

        ImGui.End();
    }
}
