﻿using System;
using System.Text.Json.Serialization;
using Fumen.Converters;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary.PerformedChart;
using static StepManiaEditor.Editor;
using static StepManiaEditor.Utils;
using static System.Diagnostics.Debug;

namespace StepManiaEditor;

/// <summary>
/// Class for rendering an autogenerated Pattern event.
/// The Pattern is rendered as an IRegion and also as a miscellaneous editor event widget.
/// </summary>
internal sealed class EditorPatternEvent : EditorEvent, IChartRegion,
	Fumen.IObserver<EditorConfig<Config>>,
	Fumen.IObserver<EditorConfig<PatternConfig>>
{
	public static readonly string EventShortDescription =
		"Patterns are automatically generated sequences of steps.";

	public static readonly string WidgetHelp =
		"Pattern.\n" +
		EventShortDescription;

	/// <summary>
	/// Definition of this an EditorPatternEvent.
	/// Serialized and deserialized with Chart save data.
	/// </summary>
	public class Definition
	{
		[JsonInclude] public Guid PatternConfigGuid;
		[JsonInclude] public Guid PerformedChartConfigGuid;
		[JsonInclude] public int Length;
		[JsonInclude] public int RandomSeed;
		[JsonInclude] public bool StartPositionInclusive;
		[JsonInclude] public bool EndPositionInclusive;
		[JsonInclude] public bool IgnorePrecedingDistribution;

		/// <summary>
		/// Returns a new Definition that is a clone of this Definition.
		/// </summary>
		public Definition Clone()
		{
			// All members are value types.
			return (Definition)MemberwiseClone();
		}

		public bool Matches(Definition other)
		{
			if (ReferenceEquals(null, other))
				return false;
			if (ReferenceEquals(this, other))
				return true;
			return PatternConfigGuid.Equals(other.PatternConfigGuid)
			       && PerformedChartConfigGuid.Equals(other.PerformedChartConfigGuid)
			       && Length == other.Length
			       && RandomSeed == other.RandomSeed
			       && StartPositionInclusive == other.StartPositionInclusive
			       && EndPositionInclusive == other.EndPositionInclusive
			       && IgnorePrecedingDistribution == other.IgnorePrecedingDistribution;
		}
	}

	/// <summary>
	/// This EditorPatternEvent's Definition;
	/// </summary>
	private Definition EventDefinition;

	/// <summary>
	/// The guid for the EditorPatternConfig that this EditorPatternEvent is using.
	/// </summary>
	[JsonInclude]
	public Guid PatternConfigGuid
	{
		get => EventDefinition.PatternConfigGuid;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			if (EventDefinition.PatternConfigGuid != value)
			{
				// When switching EditorPatternConfigs we need to observe the new one for
				// changes. We only observe the config if this EditorPatternEvent is added
				// to the chart.
				if (AddedToChart)
					GetPatternConfig().RemoveObserver(this);
				EventDefinition.PatternConfigGuid = value;
				if (AddedToChart)
					GetPatternConfig().AddObserver(this);

				// The EditorPatternEvent affects the string used in the misc event widget.
				WidthDirty = true;
			}
		}
	}

	/// <summary>
	/// The guid for the EditorPerformedChartConfig that this EditorPatternEvent is using.
	/// </summary>
	[JsonInclude]
	public Guid PerformedChartConfigGuid
	{
		get => EventDefinition.PerformedChartConfigGuid;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			if (EventDefinition.PerformedChartConfigGuid != value)
			{
				// When switching EditorPerformedChartConfigs we need to observe the new one for
				// changes. We only observe the config if this EditorPatternEvent is added
				// to the chart.
				if (AddedToChart)
					GetPerformedChartConfig().RemoveObserver(this);
				EventDefinition.PerformedChartConfigGuid = value;
				if (AddedToChart)
					GetPerformedChartConfig().AddObserver(this);

				// The EditorPerformedChartConfig affects the string used in the misc event widget.
				WidthDirty = true;
			}
		}
	}

	/// <summary>
	/// The length of this EditorPatternEvent in rows.
	/// </summary>
	[JsonInclude]
	public int Length
	{
		get => EventDefinition.Length;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			if (EventDefinition.Length != value)
			{
				var oldEndPosition = GetEndChartPosition();
				EventDefinition.Length = value;

				// Inform the EditorChart of the change so it can update its data structures.
				EditorChart.OnPatternLengthModified(this, oldEndPosition, GetEndChartPosition());
			}
		}
	}

	/// <summary>
	/// Random seed for this EditorPatternEvent.
	/// </summary>
	[JsonInclude]
	public int RandomSeed
	{
		get => EventDefinition.RandomSeed;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			EventDefinition.RandomSeed = value;
		}
	}

	/// <summary>
	/// Whether or not the start position of this EditorPatternEvent is inclusive.
	/// </summary>
	[JsonInclude]
	public bool StartPositionInclusive
	{
		get => EventDefinition.StartPositionInclusive;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			EventDefinition.StartPositionInclusive = value;
		}
	}

	/// <summary>
	/// Whether or not the end position of this EditorPatternEvent is inclusive.
	/// </summary>
	[JsonInclude]
	public bool EndPositionInclusive
	{
		get => EventDefinition.EndPositionInclusive;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			EventDefinition.EndPositionInclusive = value;
		}
	}

	/// <summary>
	/// Whether or not the this pattern should ignore preceding steps when determining
	/// how steps should be distributed.
	/// </summary>
	[JsonInclude]
	public bool IgnorePrecedingDistribution
	{
		get => EventDefinition.IgnorePrecedingDistribution;
		set
		{
			Assert(EditorChart.CanBeEdited());
			if (!EditorChart.CanBeEdited())
				return;

			EventDefinition.IgnorePrecedingDistribution = value;
		}
	}

	/// <summary>
	/// ChartRow of this EditorPatternEvent.
	/// Exposed via property to allow movement of the event.
	/// </summary>
	[JsonInclude]
	public int ChartRow
	{
		get => (int)GetChartPosition();
		set => EditorChart.MoveEvent(this, value);
	}

	#region IChartRegion Implementation

	private double RegionX, RegionY, RegionW, RegionH;

	public double GetRegionX()
	{
		return RegionX;
	}

	public double GetRegionY()
	{
		return RegionY;
	}

	public double GetRegionW()
	{
		return RegionW;
	}

	public double GetRegionH()
	{
		return RegionH;
	}

	public double GetRegionZ()
	{
		return GetChartPosition() + PatternRegionZOffset;
	}

	public void SetRegionX(double x)
	{
		RegionX = x;
	}

	public void SetRegionY(double y)
	{
		RegionY = y;
	}

	public void SetRegionW(double w)
	{
		RegionW = w;
	}

	public void SetRegionH(double h)
	{
		RegionH = h;
	}

	public double GetRegionPosition()
	{
		return GetChartPosition();
	}

	public double GetRegionDuration()
	{
		return Length;
	}

	public bool AreRegionUnitsTime()
	{
		return false;
	}

	public bool IsVisible(SpacingMode mode)
	{
		return true;
	}

	public Color GetRegionColor()
	{
		return IRegion.GetColor(PatternRegionColor, Alpha);
	}

	#endregion IChartRegion Implementation

	/// <remarks>
	/// This lazily updates the width if it is dirty.
	/// This is a bit of hack because in order to determine the width we need to call into
	/// ImGui but that is not a thread-safe operation. If we were to set the width when
	/// loading the chart for example, this could crash. By lazily setting it we avoid this
	/// problem as long as we assume the caller of GetW() happens on the main thread.
	/// </remarks>
	private double WidthInternal;

	public override double W
	{
		get
		{
			if (WidthDirty)
			{
				WidthInternal = ImGuiLayoutUtils.GetMiscEditorEventStringWidth(GetMiscEventText());
				WidthDirty = false;
			}

			return WidthInternal;
		}
		set => WidthInternal = value;
	}

	private bool WidthDirty;

	private bool AddedToChart;

	/// <summary>
	/// Constructor taking an EventConfig object.
	/// Will create an EditorPatternEvent with reasonable defaults.
	/// </summary>
	public EditorPatternEvent(EventConfig config) : base(config)
	{
		// Set up EventDefinition with reasonable defaults.
		EventDefinition = new Definition
		{
			PatternConfigGuid = PatternConfigManager.DefaultPatternConfigSixteenthsGuid,
			PerformedChartConfigGuid = PerformedChartConfigManager.DefaultPerformedChartPatternBalancedGuid,
			Length = SMCommon.RowsPerMeasure,
			RandomSeed = new Random().Next(),
			StartPositionInclusive = true,
			EndPositionInclusive = false,
			IgnorePrecedingDistribution = false,
		};

		WidthDirty = true;
	}

	/// <summary>
	/// Constructor taking an EventConfig and a specified Definition.
	/// </summary>
	public EditorPatternEvent(EventConfig config, Definition definition) : base(config)
	{
		EventDefinition = definition;

		ResetTimeBasedOnRow();

		WidthDirty = true;
	}

	/// <summary>
	/// Clones this event.
	/// </summary>
	/// <returns>Newly cloned EditorEvent.</returns>
	public override EditorEvent Clone(EditorChart chart = null)
	{
		var clone = (EditorPatternEvent)base.Clone(chart);
		clone.EventDefinition = EventDefinition.Clone();
		return clone;
	}

	/// <summary>
	/// Gets the Definition of this EditorPatternEvent.
	/// Returned Definition is a copy of this EditorPatternEvent's Definition.
	/// </summary>
	/// <returns>Definition for this EditorPatternEvent.</returns>
	public Definition GetDefinition()
	{
		return EventDefinition.Clone();
	}

	public EditorPatternConfig GetPatternConfig()
	{
		return PatternConfigManager.Instance.GetConfig(PatternConfigGuid);
	}

	public EditorPerformedChartConfig GetPerformedChartConfig()
	{
		return PerformedChartConfigManager.Instance.GetConfig(PerformedChartConfigGuid);
	}

	public string GetMiscEventText()
	{
		var patternConfig = GetPatternConfig();
		var performedChartConfig = GetPerformedChartConfig();

		var patternName = patternConfig.Name;
		if (string.IsNullOrEmpty(patternName))
			patternName = patternConfig.ToString();
		var pccName = performedChartConfig.ShortName;
		if (string.IsNullOrEmpty(pccName))
			pccName = performedChartConfig.ToString();

		return $"{patternName}, {pccName}";
	}

	public uint GetMiscEventTextColor()
	{
		var patternConfig = GetPatternConfig();
		return ArrowGraphicManager.GetArrowColorForSubdivision(EditorPatternConfig.GetBeatSubdivision(patternConfig.PatternType));
	}

	public override string GetShortTypeName()
	{
		return "Pattern";
	}

	public override bool IsMiscEvent()
	{
		return true;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return false;
	}

	public override bool IsSelectableWithModifiers()
	{
		return true;
	}

	public override double GetEndChartPosition()
	{
		return GetChartPosition() + Length;
	}

	public override double GetEndChartTime()
	{
		var endChartTime = 0.0;
		EditorChart.TryGetTimeFromChartPosition(GetEndChartPosition(), ref endChartTime);
		return endChartTime;
	}

	public override int GetLength()
	{
		return Length;
	}

	public override int GetEndRow()
	{
		return GetRow() + GetLength();
	}

	/// <summary>
	/// Gets the spacing between steps in this pattern in rows.
	/// </summary>
	/// <returns>Spacing between steps in this pattern in rows.</returns>
	public int GetStepSpacing()
	{
		var patternConfig = GetPatternConfig();
		return SMCommon.MaxValidDenominator / EditorPatternConfig.GetBeatSubdivision(patternConfig.PatternType);
	}

	/// <summary>
	/// Gets the row of the first possible step within this pattern.
	/// It is not guaranteed for this step to be within the pattern if the start position
	/// is exclusive and the pattern is shorter than its spacing.
	/// </summary>
	/// <returns>Row if the first possible step within this pattern.</returns>
	public int GetFirstStepRow()
	{
		// The first step of a pattern will be when the pattern starts if it is inclusive.
		var startRow = GetRow();
		if (StartPositionInclusive)
			return startRow;

		// If it is exclusive we need to advance one step.
		return startRow + GetStepSpacing();
	}

	/// <summary>
	/// Gets the row of the last possible step within this pattern.
	/// It is not guaranteed for this step to be within the pattern if the pattern is
	/// exclusive on both ends and the pattern is not longer than its spacing.
	/// </summary>
	/// <returns>Row of the last possible step within this pattern.</returns>
	public int GetLastStepRow()
	{
		var spacing = GetStepSpacing();
		var startRow = GetRow();
		var len = GetLength();
		var lastStepRow = startRow + len / spacing * spacing;
		if (EndPositionInclusive)
			return lastStepRow;

		// If the end position is exclusive and the last step is at that end position, we need to 
		// back up one step.
		if (lastStepRow == startRow + len)
			lastStepRow -= spacing;
		return lastStepRow;
	}

	/// <summary>
	/// Gets the number of steps within this pattern.
	/// </summary>
	/// <returns>The number of steps within this pattern.</returns>
	public int GetNumSteps()
	{
		return GetNumStepsBeforeRow(int.MaxValue);
	}

	/// <summary>
	/// Gets the number of steps within this pattern that occur before the given row.
	/// </summary>
	/// <returns>The number of steps within this pattern that occur before the given row.</returns>
	public int GetNumStepsBeforeRow(int row)
	{
		var first = GetFirstStepRow();
		var last = GetLastStepRow();
		var spacing = GetStepSpacing();
		if (last >= row)
			last -= ((last - row) / spacing + 1) * spacing;
		if (first > last)
			return 0;
		return Math.Max(0, (last - first) / GetStepSpacing() + 1);
	}

	/// <summary>
	/// Gets a unique identifier for this event to use for ImGui widgets that draw this event.
	/// </summary>
	/// <returns>Unique identifier for this event to use for ImGui widgets that draw this event.</returns>
	protected override string GetImGuiId()
	{
		return $"PatternEvent{ChartRow}";
	}

	/// <summary>
	/// Called when this event is added to its EditorChart.
	/// An event may be added and removed repeatedly with undoing and redoing actions.
	/// EditorPatternConfig events observe their underlying configs when added to a chart.
	/// </summary>
	public override void OnAddedToChart()
	{
		// When added to a chart we want to observe the configuration objects which are relevant to
		// the misc event width.
		AddedToChart = true;
		GetPerformedChartConfig().AddObserver(this);
		GetPatternConfig().AddObserver(this);
		WidthDirty = true;
		base.OnAddedToChart();
	}

	/// <summary>
	/// Called when this event is removed from its EditorChart.
	/// An event may be added and removed repeatedly with undoing and redoing actions.
	/// EditorPatternConfig events stop observing their underlying configs when removed from a chart.
	/// </summary>
	public override void OnRemovedFromChart()
	{
		// When removed from a chart we want to stop observing the configuration objects.
		AddedToChart = false;
		GetPerformedChartConfig().RemoveObserver(this);
		GetPatternConfig().RemoveObserver(this);
		base.OnRemovedFromChart();
	}

	/// <summary>
	/// Notification handler for the EditorPerformedChartConfig.
	/// </summary>
	public void OnNotify(string eventId, EditorConfig<Config> config, object payload)
	{
		switch (eventId)
		{
			// The EditorPerformedChartConfig name affects the misc event text and width.
			case EditorPerformedChartConfig.NotificationNameChanged:
			{
				WidthDirty = true;
				break;
			}
		}
	}

	/// <summary>
	/// Notification handler for the EditorPatternConfig.
	/// </summary>
	public void OnNotify(string eventId, EditorConfig<PatternConfig> config, object payload)
	{
		switch (eventId)
		{
			// The EditorPatternConfig name affects the misc event text and width.
			case EditorPatternConfig.NotificationNameChanged:
			{
				WidthDirty = true;
				break;
			}
			// The EditorPatternConfig pattern type affects the misc event text and width.
			case EditorPatternConfig.NotificationPatternTypeChanged:
			{
				WidthDirty = true;
				break;
			}
		}
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		ImGuiLayoutUtils.MiscEditorEventPatternWidget(
			GetImGuiId(),
			this,
			(int)X, (int)Y, (int)W,
			UIPatternColorRGBA,
			IsSelected(),
			Alpha,
			WidgetHelp,
			() => { EditorChart.OnPatternEventRequestEdit(this); });
	}

	public bool Matches(EditorPatternEvent other)
	{
		return base.Matches(other)
		       && EventDefinition.Matches(other.EventDefinition);
	}

	public override bool Matches(EditorEvent other)
	{
		if (other.GetType() != GetType())
			return false;
		return Matches((EditorPatternEvent)other);
	}
}
