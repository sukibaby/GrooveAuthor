﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fumen;
using Fumen.ChartDefinition;
using StepManiaEditor.AutogenConfig;
using StepManiaLibrary;
using StepManiaLibrary.PerformedChart;
using static Fumen.Converters.SMCommon;
using static StepManiaLibrary.ExpressedChart.ExpressedChart;
using Config = StepManiaLibrary.ExpressedChart.Config;

namespace StepManiaEditor;

/// <summary>
/// Action to autogenerate steps for one or more EditorPatternEvents.
/// </summary>
internal sealed class ActionAutoGeneratePatterns : EditorAction
{
	private readonly Editor Editor;
	private readonly EditorChart EditorChart;
	private readonly List<EditorPatternEvent> Patterns;
	private readonly bool UseNewSeeds;
	private readonly List<EditorEvent> DeletedEvents = new();
	private readonly List<EditorEvent> AddedEvents = new();

	public ActionAutoGeneratePatterns(
		Editor editor,
		EditorChart editorChart,
		IEnumerable<EditorPatternEvent> allPatterns,
		bool useNewSeeds) : base(true, false)
	{
		Editor = editor;
		EditorChart = editorChart;
		Patterns = new List<EditorPatternEvent>();
		Patterns.AddRange(allPatterns);
		UseNewSeeds = useNewSeeds;
	}

	public override string ToString()
	{
		if (Patterns.Count == 1)
			return $"Autogenerate {Patterns[0].GetPatternConfig().GetPrettyString()} Pattern at row {Patterns[0].ChartRow}.";
		return $"Autogenerate {Patterns.Count} Patterns.";
	}

	public override bool AffectsFile()
	{
		return true;
	}

	protected override void UndoImplementation()
	{
		// To undo this action synchronously delete the newly added events and re-add the deleted events.
		EditorChart.DeleteEvents(AddedEvents);
		EditorChart.AddEvents(DeletedEvents);
	}

	protected override void DoImplementation()
	{
		// Check for redo and avoid doing the work again.
		if (AddedEvents.Count > 0 || DeletedEvents.Count > 0)
		{
			EditorChart.DeleteEvents(DeletedEvents);
			EditorChart.AddEvents(AddedEvents);
			OnDone();
			return;
		}

		var errorString = Patterns.Count == 1 ? "Failed to generate pattern." : "Failed to generate patterns.";

		// Get the StepGraph.
		if (!Editor.GetStepGraph(EditorChart.ChartType, out var stepGraph))
		{
			Logger.Error($"{errorString} No {ImGuiUtils.GetPrettyEnumString(EditorChart.ChartType)} StepGraph is loaded.");
			OnDone();
			return;
		}

		// Get the ExpressedChart Config.
		var expressedChartConfig = ExpressedChartConfigManager.Instance.GetConfig(EditorChart.ExpressedChartConfig);
		if (expressedChartConfig == null)
		{
			Logger.Error($"{errorString} No {EditorChart.ExpressedChartConfig} Expressed Chart Config defined.");
			OnDone();
			return;
		}

		// Delete all events which overlap regions to fill based on the patterns.
		DeleteEventsOverlappingPatterns();

		// Asynchronously generate the patterns.
		DoPatternGenerationAsync(stepGraph, expressedChartConfig.Config);
	}

	/// <summary>
	/// Performs the bulk of the event generation logic.
	/// This logic is run asynchronously and when it is complete the generated EditorEvents
	/// are added back to the EditorChart synchronously.
	/// </summary>
	/// <param name="stepGraph">The StepGraph for the EditorChart.</param>
	/// <param name="expressedChartConfig">The ExpressedChart Config for the EditorChart.</param>
	private async void DoPatternGenerationAsync(StepGraph stepGraph, Config expressedChartConfig)
	{
		// Generate patterns asynchronously.
		await Task.Run(() =>
		{
			try
			{
				// Create Events from the EditorChart
				var chartEvents = EditorChart.GenerateSmEvents();

				// This could be optimized if the RedBlackTree implemented a clone method or copy constructor.
				var editorEvents = new EventTree(EditorChart);
				foreach (var editorEvent in EditorChart.EditorEvents)
				{
					editorEvents.Insert(editorEvent);
				}

				// Generate each pattern.
				GeneratePatterns(stepGraph, expressedChartConfig, chartEvents, editorEvents);
			}
			catch (Exception e)
			{
				Logger.Error($"Failed to generate patterns. {e}");
			}
		});

		// Async work is done, add the newly generated EditorEvents.
		EditorChart.AddEvents(AddedEvents);
		OnDone();
	}

	/// <summary>
	/// Generates all EditorEvents for all patterns.
	/// Does not modify the EditorChart.
	/// Accumulates new EditorEvents in AddedEvents.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="expressedChartConfig">ExpressedChart Config for the chart.</param>
	/// <param name="chartEvents">
	/// List of all Stepmania Events in the chart.
	/// The Events overlapping the pattern regions should be deleted.
	/// This method will update chartEvents with the Stepmania Events generated by the patterns.
	/// </param>
	/// <param name="editorEvents">
	/// EventTree of all current EditorEvents.
	/// This is not the EventTree owned by the EditorChart and is safe to update asynchronously.
	/// This method will update editorEvents with the EditorEvents generated by the patterns.
	/// </param>
	private void GeneratePatterns(StepGraph stepGraph, Config expressedChartConfig, List<Event> chartEvents,
		EventTree editorEvents)
	{
		// Generate EditorEvents for each pattern in order.
		for (var patternIndex = 0; patternIndex < Patterns.Count; patternIndex++)
		{
			var pattern = Patterns[patternIndex];
			var nextPattern = patternIndex < Patterns.Count - 1 ? Patterns[patternIndex + 1] : null;

			var errorString = $"Failed to generate {pattern.GetMiscEventText()} pattern at row {pattern.GetRow()}.";

			// Create an ExpressedChart.
			var expressedChart = CreateFromSMEvents(
				chartEvents,
				stepGraph,
				expressedChartConfig,
				EditorChart.Rating);
			if (expressedChart == null)
			{
				Logger.Error($"{errorString} Could not create Expressed Chart.");
				continue;
			}

			// Get the surrounding step information and counts per lane so we can provide them to the PerformedChart
			// pattern generation logic.
			var previousStepFoot = Constants.InvalidFoot;
			var previousStepTime = 0.0;
			var previousFooting = new int[Constants.NumFeet];
			var followingFooting = new int[Constants.NumFeet];
			for (var i = 0; i < Constants.NumFeet; i++)
			{
				previousFooting[i] = Constants.InvalidFoot;
				followingFooting[i] = Constants.InvalidFoot;
			}

			var currentLaneCounts = new int[stepGraph.NumArrows];
			var patternRow = pattern.GetRow();

			// Loop over all ExpressedChart search nodes.
			// The nodes give us GraphNodes, which let us determine which arrows are associated with which feet.
			var currentExpressedChartSearchNode = expressedChart.GetRootSearchNode();
			ChartSearchNode previousExpressedChartSearchNode = null;
			var foundPreviousFooting = false;
			var editorEventEnumerator = editorEvents.First();
			editorEventEnumerator.MoveNext();
			while (currentExpressedChartSearchNode != null)
			{
				// This search node follows the pattern.
				// Check for updating following footing.
				if (currentExpressedChartSearchNode.Position >= patternRow)
				{
					foundPreviousFooting = true;

					// Now that we have passed into the range of the pattern, back up to check the preceding notes.
					GetPrecedingFooting(
						stepGraph,
						previousExpressedChartSearchNode,
						editorEventEnumerator.Clone(),
						out previousStepTime,
						out previousStepFoot,
						out previousFooting);

					// Scan forward to get the following footing.
					GetFollowingFooting(
						stepGraph,
						currentExpressedChartSearchNode,
						editorEventEnumerator.Clone(),
						out followingFooting);

					// Stop the search.
					break;
				}

				// Advance the enumerator for editorEvents and accumulate steps per lane.
				while (editorEventEnumerator.IsCurrentValid()
				       && editorEventEnumerator.Current!.GetRow() <= currentExpressedChartSearchNode.Position)
				{
					if (editorEventEnumerator.Current is EditorTapNoteEvent
					    || editorEventEnumerator.Current is EditorHoldNoteEvent)
					{
						currentLaneCounts[editorEventEnumerator.Current.GetLane()]++;
					}

					editorEventEnumerator.MoveNext();
				}

				previousExpressedChartSearchNode = currentExpressedChartSearchNode;
				currentExpressedChartSearchNode = currentExpressedChartSearchNode.GetNextNode();
			}

			// In the case where no notes follow the pattern, check for finding the preceding footing.
			if (!foundPreviousFooting)
			{
				if (previousExpressedChartSearchNode != null)
				{
					GetPrecedingFooting(
						stepGraph,
						previousExpressedChartSearchNode,
						editorEventEnumerator.Clone(),
						out previousStepTime,
						out previousStepFoot,
						out previousFooting);
				}
			}

			// If there are no previous notes, use the default position.
			if (previousFooting[Constants.L] == Constants.InvalidArrowIndex)
				previousFooting[Constants.L] = stepGraph.GetRoot().State[Constants.L, Constants.DefaultFootPortion].Arrow;
			if (previousFooting[Constants.R] == Constants.InvalidArrowIndex)
				previousFooting[Constants.R] = stepGraph.GetRoot().State[Constants.R, Constants.DefaultFootPortion].Arrow;

			// Due to the above logic to assign footing to the default state it is possible
			// for both feet to be assigned to the same arrow. Correct that.
			if (previousFooting[Constants.L] == previousFooting[Constants.R])
			{
				previousFooting[Constants.L] = stepGraph.GetRoot().State[Constants.L, Constants.DefaultFootPortion].Arrow;
				previousFooting[Constants.R] = stepGraph.GetRoot().State[Constants.R, Constants.DefaultFootPortion].Arrow;
			}

			// If we don't know what foot to start on, start on the right foot.
			if (previousStepFoot == Constants.InvalidFoot)
			{
				previousStepFoot = Constants.L;
			}

			// Create a PerformedChart section for the Pattern.
			var performedChart = PerformedChart.CreateWithPattern(stepGraph,
				pattern.GetPatternConfig().Config,
				pattern.GetPerformedChartConfig().Config,
				pattern.GetRow(),
				pattern.GetEndRow(),
				pattern.EndPositionInclusive,
				UseNewSeeds ? new Random().Next() : pattern.RandomSeed,
				previousStepFoot,
				previousStepTime,
				previousFooting,
				followingFooting,
				currentLaneCounts,
				chartEvents,
				pattern.GetMiscEventText());
			if (performedChart == null)
			{
				Logger.Error($"{errorString} Could not create Performed Chart.");
				continue;
			}

			// Convert this PerformedChart section to Stepmania Events.
			var smEvents = performedChart.CreateSMChartEvents();
			var smEventsToAdd = smEvents;

			// Check for excluding some Events. It is possible that future patterns will
			// overlap this pattern. In that case we do not want to add the notes from
			// this pattern which overlap, and we instead want to let the next pattern
			// generate those notes.
			if (nextPattern != null
			    && (nextPattern.GetRow() < pattern.GetEndRow()
			        || (pattern.EndPositionInclusive && nextPattern.GetRow() == pattern.GetEndRow())))
			{
				smEventsToAdd = new List<Event>();
				var nextPatternRow = nextPattern.GetRow();
				foreach (var smEvent in smEvents)
				{
					if (smEvent.IntegerPosition >= nextPatternRow)
						break;
					smEventsToAdd.Add(smEvent);
				}
			}

			// Update the running list of all Events.
			chartEvents.AddRange(smEventsToAdd);
			chartEvents.Sort(new SMEventComparer());
			SetEventTimeAndMetricPositionsFromRows(chartEvents);

			// Convert new events to EditorEvents.
			var newEditorEvents = new List<EditorEvent>();
			foreach (var smEvent in smEventsToAdd)
			{
				var newEditorEvent = EditorEvent.CreateEvent(EventConfig.CreateConfig(EditorChart, smEvent));
				newEditorEvents.Add(EditorEvent.CreateEvent(EventConfig.CreateConfig(EditorChart, smEvent)));
				editorEvents.Insert(newEditorEvent);
			}

			// Update the running list of all added EditorEvents.
			AddedEvents.AddRange(newEditorEvents);
		}
	}

	/// <summary>
	/// Helper function to get the preceding footing of a pattern.
	/// If preceding steps are brackets only the DefaultFootPortion (Heel)'s lane will be used.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="node">The ChartSearchNode of the last event preceding the pattern.</param>
	/// <param name="editorEventEnumerator">
	/// The EditorEvent enumerator of the last EditorEvent preceding the pattern.
	/// </param>
	/// <param name="previousStepTime">
	/// Out parameter to record the time of the most recent preceding step.
	/// </param>
	/// <param name="previousStepFoot">
	/// Out parameter to record the foot used to step on the most recent preceding step.
	/// </param>
	/// <param name="previousFooting">
	/// Out parameter to record the lane stepped on per foot of the preceding steps.
	/// </param>
	private static void GetPrecedingFooting(
		StepGraph stepGraph,
		ChartSearchNode node,
		RedBlackTree<EditorEvent>.IRedBlackTreeEnumerator editorEventEnumerator,
		out double previousStepTime,
		out int previousStepFoot,
		out int[] previousFooting)
	{
		// Initialize out parameters.
		previousStepFoot = Constants.InvalidFoot;
		previousStepTime = 0.0;
		previousFooting = new int[Constants.NumFeet];
		for (var i = 0; i < Constants.NumFeet; i++)
		{
			previousFooting[i] = Constants.InvalidFoot;
		}

		// Scan backwards.
		var numFeetFound = 0;
		var positionOfCurrentSteps = -1;
		var currentSteppedLanes = new bool[stepGraph.NumArrows];
		while (node != null)
		{
			// If we have scanned backwards into a new row, update the currently stepped on lanes for that row.
			CheckAndUpdateCurrentSteppedLanes(stepGraph, node, editorEventEnumerator, ref positionOfCurrentSteps,
				ref currentSteppedLanes, false);

			// Update the tracked footing based on the currently stepped on lanes.
			CheckAndUpdateFooting(stepGraph, node, previousFooting, currentSteppedLanes, ref numFeetFound, ref previousStepFoot,
				ref previousStepTime);

			if (numFeetFound == Constants.NumFeet)
				break;

			// Advance.
			node = node.PreviousNode;
		}
	}

	/// <summary>
	/// Helper function to get the following footing of a pattern.
	/// If following steps are brackets only the DefaultFootPortion (Heel)'s lane will be used.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the associated chart.</param>
	/// <param name="node">The ChartSearchNode of the first event following the pattern.</param>
	/// <param name="editorEventEnumerator">
	/// The EditorEvent enumerator of the first EditorEvent following the pattern.
	/// </param>
	/// <param name="followingFooting">
	/// Out parameter to record the lane stepped on per foot of the following steps.
	/// </param>
	private static void GetFollowingFooting(
		StepGraph stepGraph,
		ChartSearchNode node,
		RedBlackTree<EditorEvent>.IRedBlackTreeEnumerator editorEventEnumerator,
		out int[] followingFooting)
	{
		// Initialize out parameters.
		followingFooting = new int[Constants.NumFeet];
		for (var i = 0; i < Constants.NumFeet; i++)
		{
			followingFooting[i] = Constants.InvalidFoot;
		}

		// Unused variables, but they simplify the common footing update logic.
		var followingStepFoot = Constants.InvalidFoot;
		var followingStepTime = 0.0;

		// The enumerator is already beyond the pattern. We want to back up one to easily examine
		// the steps following the pattern.

		// If the enumerator has moved beyond the final note, back it up one.
		if (!editorEventEnumerator.IsCurrentValid())
			editorEventEnumerator.MovePrev();

		// Back up until we precede the row following the pattern.
		while (editorEventEnumerator.IsCurrentValid() && editorEventEnumerator.Current.GetRow() >= node.Position)
		{
			if (!editorEventEnumerator.MovePrev())
			{
				editorEventEnumerator.MoveNext();
				break;
			}
		}

		// Scan forwards.
		var numFeetFound = 0;
		var positionOfCurrentSteps = -1;
		var currentSteppedLanes = new bool[stepGraph.NumArrows];
		while (node != null)
		{
			// If we have scanned forward into a new row, update the currently stepped on lanes for that row.
			CheckAndUpdateCurrentSteppedLanes(stepGraph, node, editorEventEnumerator, ref positionOfCurrentSteps,
				ref currentSteppedLanes, true);

			// Update the tracked footing based on the currently stepped on lanes.
			CheckAndUpdateFooting(stepGraph, node, followingFooting, currentSteppedLanes, ref numFeetFound, ref followingStepFoot,
				ref followingStepTime);

			if (numFeetFound == Constants.NumFeet)
				break;

			// Advance.
			node = node.GetNextNode();
		}
	}

	/// <summary>
	/// Helper function for updating an array of currently stepped on lanes when scanning and the row changes.
	/// The currently stepped on lanes are used for determining footing when comparing against a GraphNode.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the chart.</param>
	/// <param name="node">
	/// Current ChartSearchNode. If the position of the current steps doesn't equal this node's position
	/// then the currentSteppedLanes will be updated accordingly.
	/// </param>
	/// <param name="editorEventEnumerator">
	/// Enumerator of the EditorEvents to use for scanning to determine which lanes are stepped on.
	/// </param>
	/// <param name="positionOfCurrentSteps">
	/// Last position of the currentSteppedLanes. Will be updated if currentSteppedLanes are updated.
	/// </param>
	/// <param name="currentSteppedLanes">
	/// Array of bools, one per lane. This will be updated to reflect which lanes have steps on them
	/// if the positionOfCurrentSteps is old and needs to be updated based on the given node's position.
	/// </param>
	/// <param name="scanForward">
	/// If true, scan forward for following steps. If false, scan backwards for preceding steps.
	/// </param>
	private static void CheckAndUpdateCurrentSteppedLanes(
		StepGraph stepGraph,
		ChartSearchNode node,
		RedBlackTree<EditorEvent>.IRedBlackTreeEnumerator editorEventEnumerator,
		ref int positionOfCurrentSteps,
		ref bool[] currentSteppedLanes,
		bool scanForward)
	{
		// Determine the steps which occur at the row of this node, so we can assign feet to them.
		if (positionOfCurrentSteps != node.Position)
		{
			// Clear stepped lanes.
			for (var i = 0; i < stepGraph.NumArrows; i++)
				currentSteppedLanes[i] = false;

			// Scan the current row, recording the lanes being stepped on at this position.
			while (editorEventEnumerator.IsCurrentValid() &&
			       (scanForward
				       ? editorEventEnumerator.Current.GetRow() <= node.Position
				       : editorEventEnumerator.Current.GetRow() >= node.Position))
			{
				if (editorEventEnumerator.Current.GetRow() == node.Position)
				{
					if (editorEventEnumerator.Current is EditorTapNoteEvent ||
					    editorEventEnumerator.Current is EditorHoldNoteEvent)
					{
						currentSteppedLanes[editorEventEnumerator.Current.GetLane()] = true;
					}
				}

				if (scanForward)
					editorEventEnumerator.MoveNext();
				else
					editorEventEnumerator.MovePrev();
			}

			// Update the position we have recorded steps for.
			positionOfCurrentSteps = node.Position;
		}
	}

	/// <summary>
	/// Helper function to update preceding or following footing.
	/// </summary>
	/// <param name="stepGraph">StepGraph of the chart.</param>
	/// <param name="node">Current ChartSearchNode.</param>
	/// <param name="footing">
	/// Array of lanes per foot representing previous or following footing to fill.
	/// Will be updated as footing is found.
	/// </param>
	/// <param name="steppedLanes">
	/// Array of bools per lane representing which lanes are currently stepped on.
	/// </param>
	/// <param name="numFeetFound">
	/// Number of feet whose footing is currently found. Will be updated as footing
	/// is found.
	/// </param>
	/// <param name="stepFoot">
	/// Foot of the first preceding or following step to set.
	/// </param>
	/// <param name="stepFootTime">
	/// Time of the first preceding of following step to set.
	/// </param>
	private static void CheckAndUpdateFooting(
		StepGraph stepGraph,
		ChartSearchNode node,
		int[] footing,
		bool[] steppedLanes,
		ref int numFeetFound,
		ref int stepFoot,
		ref double stepFootTime)
	{
		// With the stepped on lanes known, use the GraphNodes to determine which foot stepped
		// on each lane.
		if (node.PreviousLink != null && !node.PreviousLink.GraphLink.IsRelease())
		{
			for (var f = 0; f < Constants.NumFeet; f++)
			{
				if (footing[f] != Constants.InvalidFoot)
					continue;
				for (var p = 0; p < Constants.NumFootPortions; p++)
				{
					if (footing[f] != Constants.InvalidFoot)
						continue;

					if (node.GraphNode.State[f, p].State != GraphArrowState.Lifted)
					{
						for (var a = 0; a < stepGraph.NumArrows; a++)
						{
							if (steppedLanes[a] && a == node.GraphNode.State[f, p].Arrow)
							{
								if (stepFoot == Constants.InvalidFoot)
								{
									stepFoot = f;
									stepFootTime = node.TimeSeconds;
								}

								footing[f] = node.GraphNode.State[f, p].Arrow;
								numFeetFound++;
								break;
							}
						}
					}

					if (numFeetFound == Constants.NumFeet)
						break;
				}

				if (numFeetFound == Constants.NumFeet)
					break;
			}
		}
	}

	/// <summary>
	/// Deletes all EditorEvents in the EditorChart which intersect any of the Patterns.
	/// Stores all deleted EditorEvents in DeletedEvents.
	/// </summary>
	private void DeleteEventsOverlappingPatterns()
	{
		foreach (var pattern in Patterns)
		{
			var deletedEventsForPattern = new List<EditorEvent>();
			var startRow = pattern.GetRow();
			var endRow = pattern.GetEndRow();

			// Accumulate any holds which overlap the start of the pattern.
			var overlappingHolds = EditorChart.GetHoldsOverlapping(startRow);
			foreach (var overlappingHold in overlappingHolds)
			{
				if (overlappingHold != null)
				{
					deletedEventsForPattern.Add(overlappingHold);
				}
			}

			// Accumulate taps, holds, and mines which fall within the pattern region.
			var enumerator = EditorChart.EditorEvents.FindBestByPosition(startRow);
			if (enumerator != null && enumerator.MoveNext())
			{
				var row = enumerator.Current!.GetRow();
				while (row <= endRow)
				{
					if (row >= startRow &&
					    (enumerator.Current is EditorTapNoteEvent
					     || enumerator.Current is EditorMineNoteEvent
					     || enumerator.Current is EditorHoldNoteEvent))
					{
						if (row < endRow || (pattern.EndPositionInclusive && row == endRow))
						{
							deletedEventsForPattern.Add(enumerator.Current);
						}
					}

					if (!enumerator.MoveNext())
						break;
					row = enumerator.Current.GetRow();
				}
			}

			// Store the deleted events for later undoing.
			DeletedEvents.AddRange(deletedEventsForPattern);

			// Delete the events now rather than waiting to accumulate all events.
			// These prevents accidentally trying to delete the same event more than once
			// when patterns overlap.
			EditorChart.DeleteEvents(deletedEventsForPattern);
		}
	}
}
