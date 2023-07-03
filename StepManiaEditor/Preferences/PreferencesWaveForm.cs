﻿using System.Numerics;
using System.Text.Json.Serialization;
using static Fumen.FumenExtensions;

namespace StepManiaEditor;

/// <summary>
/// Preferences for the WaveForm.
/// </summary>
internal sealed class PreferencesWaveForm
{
	// Default values.
	public const bool DefaultShowWaveForm = true;
	public const bool DefaultEnableWaveForm = true;
	public const bool DefaultWaveFormScaleXWhenZooming = false;

	public const UIWaveFormPreferences.SparseColorOption DefaultWaveFormSparseColorOption =
		UIWaveFormPreferences.SparseColorOption.DarkerDenseColor;

	public const float DefaultWaveFormSparseColorScale = 0.8f;
	public static readonly Vector4 DefaultWaveFormDenseColor = new(0.0f, 0.389f, 0.183f, 0.8f);
	public static readonly Vector4 DefaultWaveFormSparseColor = new(0.0f, 0.350f, 0.164f, 0.8f);
	public static readonly Vector4 DefaultWaveFormBackgroundColor = new(0.0f, 0.0f, 0.0f, 0.0f);
	public const float DefaultWaveFormMaxXPercentagePerChannel = 0.9f;
	public const int DefaultWaveFormLoadingMaxParallelism = 8;
	public const float DefaultDenseScale = 6.0f;
	public const bool DefaultAntiAlias = true;
	public const float DefaultAntiAliasSubpix = 0.2f;
	public const float DefaultAntiAliasEdgeThreshold = 0.166f;
	public const float DefaultAntiAliasEdgeThresholdMin = 0.0833f;

	// Preferences.
	[JsonInclude] public bool ShowWaveFormPreferencesWindow;
	[JsonInclude] public bool ShowWaveForm = DefaultShowWaveForm;
	[JsonInclude] public bool EnableWaveForm = DefaultEnableWaveForm;
	[JsonInclude] public bool WaveFormScaleXWhenZooming = DefaultWaveFormScaleXWhenZooming;
	[JsonInclude] public UIWaveFormPreferences.SparseColorOption WaveFormSparseColorOption = DefaultWaveFormSparseColorOption;
	[JsonInclude] public float WaveFormSparseColorScale = DefaultWaveFormSparseColorScale;
	[JsonInclude] public Vector4 WaveFormDenseColor = DefaultWaveFormDenseColor;
	[JsonInclude] public Vector4 WaveFormSparseColor = DefaultWaveFormSparseColor;
	[JsonInclude] public Vector4 WaveFormBackgroundColor = DefaultWaveFormBackgroundColor;
	[JsonInclude] public float WaveFormMaxXPercentagePerChannel = DefaultWaveFormMaxXPercentagePerChannel;
	[JsonInclude] public int WaveFormLoadingMaxParallelism = DefaultWaveFormLoadingMaxParallelism;
	[JsonInclude] public float DenseScale = DefaultDenseScale;
	[JsonInclude] public bool AntiAlias = DefaultAntiAlias;
	[JsonInclude] public float AntiAliasSubpix = DefaultAntiAliasSubpix;
	[JsonInclude] public float AntiAliasEdgeThreshold = DefaultAntiAliasEdgeThreshold;
	[JsonInclude] public float AntiAliasEdgeThresholdMin = DefaultAntiAliasEdgeThresholdMin;

	public bool IsUsingDefaults()
	{
		return ShowWaveForm == DefaultShowWaveForm
		       && EnableWaveForm == DefaultEnableWaveForm
		       && WaveFormScaleXWhenZooming == DefaultWaveFormScaleXWhenZooming
		       && WaveFormSparseColorOption == DefaultWaveFormSparseColorOption
		       && WaveFormSparseColorScale.FloatEquals(DefaultWaveFormSparseColorScale)
		       && WaveFormDenseColor.Equals(DefaultWaveFormDenseColor)
		       && WaveFormSparseColor.Equals(DefaultWaveFormSparseColor)
		       && WaveFormBackgroundColor.Equals(DefaultWaveFormBackgroundColor)
		       && WaveFormMaxXPercentagePerChannel.FloatEquals(DefaultWaveFormMaxXPercentagePerChannel)
		       && WaveFormLoadingMaxParallelism == DefaultWaveFormLoadingMaxParallelism
		       && DenseScale.FloatEquals(DefaultDenseScale)
		       && AntiAlias == DefaultAntiAlias
		       && AntiAliasSubpix.FloatEquals(DefaultAntiAliasSubpix)
		       && AntiAliasEdgeThreshold.FloatEquals(DefaultAntiAliasEdgeThreshold)
		       && AntiAliasEdgeThresholdMin.FloatEquals(DefaultAntiAliasEdgeThresholdMin);
	}

	public void RestoreDefaults()
	{
		// Don't enqueue an action if it would not have any effect.
		if (IsUsingDefaults())
			return;
		ActionQueue.Instance.Do(new ActionRestoreWaveFormPreferenceDefaults());
	}
}

/// <summary>
/// Action to restore WaveForm preferences to their default values.
/// </summary>
internal sealed class ActionRestoreWaveFormPreferenceDefaults : EditorAction
{
	private readonly bool PreviousShowWaveForm;
	private readonly bool PreviousEnableWaveForm;
	private readonly bool PreviousWaveFormScaleXWhenZooming;
	private readonly UIWaveFormPreferences.SparseColorOption PreviousWaveFormSparseColorOption;
	private readonly float PreviousWaveFormSparseColorScale;
	private readonly Vector4 PreviousWaveFormDenseColor;
	private readonly Vector4 PreviousWaveFormSparseColor;
	private readonly Vector4 PreviousWaveFormBackgroundColor;
	private readonly float PreviousWaveFormMaxXPercentagePerChannel;
	private readonly int PreviousWaveFormLoadingMaxParallelism;
	private readonly float PreviousDenseScale;
	private readonly bool PreviousAntiAlias;
	private readonly float PreviousAntiAliasSubpix;
	private readonly float PreviousAntiAliasEdgeThreshold;
	private readonly float PreviousAntiAliasEdgeThresholdMin;

	public ActionRestoreWaveFormPreferenceDefaults() : base(false, false)
	{
		var p = Preferences.Instance.PreferencesWaveForm;
		PreviousShowWaveForm = p.ShowWaveForm;
		PreviousEnableWaveForm = p.EnableWaveForm;
		PreviousWaveFormScaleXWhenZooming = p.WaveFormScaleXWhenZooming;
		PreviousWaveFormSparseColorOption = p.WaveFormSparseColorOption;
		PreviousWaveFormSparseColorScale = p.WaveFormSparseColorScale;
		PreviousWaveFormDenseColor = p.WaveFormDenseColor;
		PreviousWaveFormSparseColor = p.WaveFormSparseColor;
		PreviousWaveFormBackgroundColor = p.WaveFormBackgroundColor;
		PreviousWaveFormMaxXPercentagePerChannel = p.WaveFormMaxXPercentagePerChannel;
		PreviousWaveFormLoadingMaxParallelism = p.WaveFormLoadingMaxParallelism;
		PreviousDenseScale = p.DenseScale;
		PreviousAntiAlias = p.AntiAlias;
		PreviousAntiAliasSubpix = p.AntiAliasSubpix;
		PreviousAntiAliasEdgeThreshold = p.AntiAliasEdgeThreshold;
		PreviousAntiAliasEdgeThresholdMin = p.AntiAliasEdgeThresholdMin;
	}

	public override bool AffectsFile()
	{
		return false;
	}

	public override string ToString()
	{
		return "Restore Waveform default preferences.";
	}

	protected override void DoImplementation()
	{
		var p = Preferences.Instance.PreferencesWaveForm;
		p.ShowWaveForm = PreferencesWaveForm.DefaultShowWaveForm;
		p.EnableWaveForm = PreferencesWaveForm.DefaultEnableWaveForm;
		p.WaveFormScaleXWhenZooming = PreferencesWaveForm.DefaultWaveFormScaleXWhenZooming;
		p.WaveFormSparseColorOption = PreferencesWaveForm.DefaultWaveFormSparseColorOption;
		p.WaveFormSparseColorScale = PreferencesWaveForm.DefaultWaveFormSparseColorScale;
		p.WaveFormDenseColor = PreferencesWaveForm.DefaultWaveFormDenseColor;
		p.WaveFormSparseColor = PreferencesWaveForm.DefaultWaveFormSparseColor;
		p.WaveFormBackgroundColor = PreferencesWaveForm.DefaultWaveFormBackgroundColor;
		p.WaveFormMaxXPercentagePerChannel = PreferencesWaveForm.DefaultWaveFormMaxXPercentagePerChannel;
		p.WaveFormLoadingMaxParallelism = PreferencesWaveForm.DefaultWaveFormLoadingMaxParallelism;
		p.DenseScale = PreferencesWaveForm.DefaultDenseScale;
		p.AntiAlias = PreferencesWaveForm.DefaultAntiAlias;
		p.AntiAliasSubpix = PreferencesWaveForm.DefaultAntiAliasSubpix;
		p.AntiAliasEdgeThreshold = PreferencesWaveForm.DefaultAntiAliasEdgeThreshold;
		p.AntiAliasEdgeThresholdMin = PreferencesWaveForm.DefaultAntiAliasEdgeThresholdMin;
	}

	protected override void UndoImplementation()
	{
		var p = Preferences.Instance.PreferencesWaveForm;
		p.ShowWaveForm = PreviousShowWaveForm;
		p.EnableWaveForm = PreviousEnableWaveForm;
		p.WaveFormScaleXWhenZooming = PreviousWaveFormScaleXWhenZooming;
		p.WaveFormSparseColorOption = PreviousWaveFormSparseColorOption;
		p.WaveFormSparseColorScale = PreviousWaveFormSparseColorScale;
		p.WaveFormDenseColor = PreviousWaveFormDenseColor;
		p.WaveFormSparseColor = PreviousWaveFormSparseColor;
		p.WaveFormBackgroundColor = PreviousWaveFormBackgroundColor;
		p.WaveFormMaxXPercentagePerChannel = PreviousWaveFormMaxXPercentagePerChannel;
		p.WaveFormLoadingMaxParallelism = PreviousWaveFormLoadingMaxParallelism;
		p.DenseScale = PreviousDenseScale;
		p.AntiAlias = PreviousAntiAlias;
		p.AntiAliasSubpix = PreviousAntiAliasSubpix;
		p.AntiAliasEdgeThreshold = PreviousAntiAliasEdgeThreshold;
		p.AntiAliasEdgeThresholdMin = PreviousAntiAliasEdgeThresholdMin;
	}
}
