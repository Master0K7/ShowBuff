using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Attributes;
using SharpDX;
using System.Collections.Generic;

namespace ShowBuff_exileapi;

public class ShowBuffSetting
{
    [Menu("Buff Name", "Exact buff name from DevTree")]
    public TextNode BuffName { get; set; } = new TextNode("");

    [Menu("Display Name", "Name to display")]
    public TextNode DisplayName { get; set; } = new TextNode("");

    [Menu("Show", "Show this buff")]
    public ToggleNode Show { get; set; } = new ToggleNode(false);

    [Menu("Text Color")]
    public ColorNode TextColor { get; set; } = new ColorNode(Color.Orange);

    [Menu("Min Stacks", "Show only if stacks are greater than this number")]
    public RangeNode<int> MinStacks { get; set; } = new RangeNode<int>(1, 0, 100);

    [Menu("Position X", "X offset when using fixed position")]
    public RangeNode<int> PositionX { get; set; } = new RangeNode<int>(0, -1500, 1500);

    [Menu("Position Y", "Y offset when using fixed position")]
    public RangeNode<int> PositionY { get; set; } = new RangeNode<int>(-30, -1500, 1500);

    [Menu("Use Head Position", "If disabled — use fixed screen position")]
    public ToggleNode UseHeadPosition { get; set; } = new ToggleNode(true);

    [Menu("Hide Stack Count", "Show only name without count")]
    public ToggleNode HideStackCount { get; set; } = new ToggleNode(false);
}

public class ShowBuffSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(true);

    [Menu("General Settings")] public EmptyNode GeneralHeader { get; set; } = new EmptyNode();

    [Menu("Language", "English/Russian interface")]
    public ToggleNode UseEnglish { get; set; } = new ToggleNode(true);

    [Menu("Font Size", "Font size for text rendering")]
    public RangeNode<float> FontSize { get; set; } = new RangeNode<float>(1.0f, 0.5f, 3.0f);

    [Menu("Height Offset", "Vertical offset above head, px")]
    public RangeNode<int> HeightOffset { get; set; } = new RangeNode<int>(60, 0, 200);

    [Menu("Show Background", "Add semi-transparent background for readability")]
    public ToggleNode ShowBackground { get; set; } = new ToggleNode(true);

    [Menu("Background Color")] public ColorNode BackgroundColor { get; set; } = new ColorNode(new Color(0, 0, 0, 128));

    [Menu("Show in Hideout", "Render in hideout")]
    public ToggleNode ShowInHideout { get; set; } = new ToggleNode(false);

    [Menu("Buff Settings")] public EmptyNode BuffsHeader { get; set; } = new EmptyNode();

    public List<ShowBuffSetting> BuffSettings { get; set; } = new List<ShowBuffSetting>();

    [Menu("Show All Buffs Window", "Open separate window with search, sort and all detected buffs with Type values")]
    public ToggleNode ShowAllBuffsWindow { get; set; } = new ToggleNode(false);
    
    [Menu("Freeze Buff List", "Stop updating detected buffs list (useful for catching short-duration buffs)")]
    public ToggleNode FreezeBuffList { get; set; } = new ToggleNode(false);
}