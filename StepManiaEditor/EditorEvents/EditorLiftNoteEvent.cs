﻿using Fumen.ChartDefinition;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameExtensions;
using static StepManiaEditor.Utils;

namespace StepManiaEditor;

internal sealed class EditorLiftNoteEvent : EditorEvent
{
	private readonly LaneTapNote LaneTapNote;

	public EditorLiftNoteEvent(EventConfig config, LaneTapNote chartEvent) : base(config)
	{
		LaneTapNote = chartEvent;
	}

	public override int GetLane()
	{
		return LaneTapNote.Lane;
	}

	public override bool IsMiscEvent()
	{
		return false;
	}

	public override bool IsSelectableWithoutModifiers()
	{
		return true;
	}

	public override bool IsSelectableWithModifiers()
	{
		return false;
	}

	public override void Draw(TextureAtlas textureAtlas, SpriteBatch spriteBatch, ArrowGraphicManager arrowGraphicManager)
	{
		var alpha = IsBeingEdited() ? ActiveEditEventAlpha : Alpha;
		if (alpha <= 0.0f)
			return;

		// Draw the arrow.
		var (textureId, rot) = arrowGraphicManager.GetArrowTexture(LaneTapNote.IntegerPosition, LaneTapNote.Lane, IsSelected());
		textureAtlas.Draw(
			textureId,
			spriteBatch,
			new Vector2((float)X, (float)Y),
			Scale,
			rot,
			alpha);

		// Draw the lift marker. Do not draw it with the selection overlay as it looks weird.
		var liftTextureId = ArrowGraphicManager.GetLiftMarkerTexture(LaneTapNote.IntegerPosition, LaneTapNote.Lane, false);
		var (arrowW, arrowH) = textureAtlas.GetDimensions(textureId);
		var (markerW, markerH) = textureAtlas.GetDimensions(liftTextureId);
		var markerX = X + (arrowW - markerW) * 0.5 * Scale;
		var markerY = Y + (arrowH - markerH) * 0.5 * Scale;
		textureAtlas.Draw(
			liftTextureId,
			spriteBatch,
			new Vector2((float)markerX, (float)markerY),
			Scale,
			0.0f,
			alpha);
	}
}
