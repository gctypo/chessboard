﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ChessBoard;

namespace ChessBoardWPFDisplay
{
    public class ChessBoardGame : CanvasGame
    {
		public Sprite BoardSprite
		{ get; }

		public Sprite NotationOverlay
		{ get; }

		public double Scale
		{ get; private set; }

		public List<RenderedPiece> RenderedPieces
		{ get; } = new List<RenderedPiece>();

		public RenderedPiece GrabbedPiece
		{ get; set; }

		public GhostPiece GrabbedGhost
		{ get; set; }

		public Vector2 GrabOffset
		{ get; set; }

		public Board Board
		{ get; }

		public bool EnforceTurns
		{ get; set; }

		public bool ShowHistory
		{ get; set; }

		public bool ShowNotationOverlay
		{
			get => NotationOverlay.Visible;
			set => NotationOverlay.Visible = value;
		}

		public bool DebugMode
		{
			get => _debugMode;
			set
			{
				foreach (RenderedPiece rp in RenderedPieces)
				{
					rp.ShowDebug = value;
					rp.Refresh();
				}

				if (GrabbedGhost != null)
				{
					GrabbedGhost.ShowDebug = value;
					GrabbedGhost.Refresh();
				}

				_debugMode = value;
			}
		}
		private bool _debugMode;

		public List<Rectangle> GrabbedValidLocations
		{ get; } = new List<Rectangle>();

		public ChessBoardGame(Canvas control) : base(control)
		{
			Scale = 0.5;

			BoardSprite = new Sprite(Canvas, "img/qualityboard_1024.png", new Vector2(50, 50), Scale);
			NotationOverlay = new Sprite(Canvas, "img/notationoverlay_1024.png", new Vector2(50, 50), Scale, 0.65);
			Panel.SetZIndex(NotationOverlay.Control, 1100);

			Sprites.Add(BoardSprite);
			Sprites.Add(NotationOverlay);

			Move.PreferredNotation = NotationType.Algebraic;
			
			Board = new Board();
			Board.OnPieceMoved += onPieceMoved;

			SetUpPiecesFromBoard();

			EnforceTurns = true;
			ShowHistory = true;
			ShowNotationOverlay = false;
		}

		public override void Initialize(RoutedEventArgs e)
		{
			BoardSprite.Initialize();
			NotationOverlay.Initialize();

			foreach (RenderedPiece rp in RenderedPieces)
			{
				rp.Initialize();
			}

			base.Initialize(e);
		}

		public void SetUpPiecesFromBoard(bool reset = false)
		{
			RenderedPieces.Clear();

			foreach (Piece p in Board.Pieces)
			{
				if (p != null)
				{
					RenderedPiece rp = new RenderedPiece(p, BoardSprite, Canvas);
					if (reset)
					{
						rp.Initialize();
					}

					RenderedPieces.Add(rp);
				}
			}
		}

		public void GrabPiece(RenderedPiece piece, MouseButtonEventArgs e)
		{
			if (EnforceTurns && piece.Piece.Side != Board.Turn)
			{
				return;
			}

			GrabbedPiece = piece;
			GrabOffset = e.GetPosition(piece.Sprite.Control);
			Panel.SetZIndex(GrabbedPiece.Sprite.Control, 1000);

			foreach (Rectangle r in GrabbedValidLocations)
			{
				Canvas.Children.Remove(r);
			}
			GrabbedValidLocations.Clear();

			GrabbedGhost = new GhostPiece(GrabbedPiece, Canvas);
			GrabbedGhost.Initialize();
		}

		public void DropPiece(Point boardPos)
		{
			if (GrabbedPiece == null)
			{
				return;
			}

			Vector2 posCenter = GrabbedPiece.RenderedPos + GrabbedPiece.Sprite.ActualSize / 2.0;
			Tile tile = getTile(posCenter);

			if (Board.Validator.IsMovementValid(GrabbedPiece.Piece, tile, out Move move))
			{
				Board.Moves.Add(move);
				if (move is MovePromotion mpPre)
				{
					PromotionsWindow promotionDialog = new PromotionsWindow();
					promotionDialog.ShowDialog();
					mpPre.Promotion = promotionDialog.Result;
				}

				move.DoMove();
				move.AppendCheckNotation();

				if (move is MovePromotion mpPost)
				{
					RenderedPiece promoted = RenderedPieces.First(rp => rp.Piece == move.Piece);
					promoted.ChangePiece(mpPost.Promotion, mpPost.Piece.Side, Canvas);
				}

				foreach (RenderedPiece rp in RenderedPieces)
				{
					rp.RenderedPos = getAbsCoords(rp.Piece.Position);
					rp.Refresh();
				}
			}
			else
			{
				GrabbedPiece.RenderedPos = getAbsCoords(GrabbedPiece.Piece.Position);
				GrabbedPiece.Refresh();
			}

			Panel.SetZIndex(GrabbedPiece.Sprite.Control, 10);
			
			GrabbedPiece = null;

			GrabbedGhost.Disconnect(Canvas);
			GrabbedGhost = null;
			DeleteAllGhosts();

			foreach (Rectangle r in GrabbedValidLocations)
			{
				Canvas.Children.Remove(r);
			}
			GrabbedValidLocations.Clear();
		}

		public override void Refresh(MouseEventArgs e)
		{
			if (GrabbedPiece != null)
			{
				GrabbedPiece.RenderedPos = e.GetPositionV() - GrabOffset;
				GrabbedPiece.Refresh();
				GrabbedGhost.Refresh();

				foreach (Rectangle r in GrabbedValidLocations)
				{
					Canvas.Children.Remove(r);
				}
				GrabbedValidLocations.Clear();

				List<Move> valid = Board.Validator.GetValidLocations(GrabbedPiece.Piece);
				if (valid != null)
				{
					foreach (Move m in valid)
					{
						Rectangle r = new Rectangle() {
							Width = GrabbedPiece.Sprite.ActualWidth,
							Height = GrabbedPiece.Sprite.ActualHeight,
							Fill = new SolidColorBrush(Colors.Blue),
							Opacity = 0.4
						};
						r.SetPos(getAbsCoords(m.To));
						Panel.SetZIndex(r, 1);
						Canvas.Children.Add(r);
						GrabbedValidLocations.Add(r);
					}
				}
			}

			base.Refresh(e);
		}

		public void Reset()
		{
			Board.Reset();

			foreach (RenderedPiece rp in RenderedPieces)
			{
				rp.Disconnect(Canvas);
			}

			SetUpPiecesFromBoard(true);

			GrabbedPiece?.Disconnect(Canvas);
			GrabbedPiece = null;

			GrabbedGhost?.Disconnect(Canvas);
			GrabbedGhost = null;
			DeleteAllGhosts();

			foreach (Rectangle r in GrabbedValidLocations)
			{
				Canvas.Children.Remove(r);
			}
			GrabbedValidLocations.Clear();
		}

		private Tile getTile(Vector2 posAbs)
		{
			Vector2 relPos = posAbs - BoardSprite.Position;
			
			int tx = (int)(relPos.X / BoardSprite.ActualWidth * 8.0);
			int ty = 7 - (int)(relPos.Y / BoardSprite.ActualHeight * 8.0);

			return new Tile(ty, tx).ClampValid();
		}

		private Vector2 getAbsCoords(Tile tile)
		{
			return new Vector2(tile.Column / 8.0 * BoardSprite.ActualWidth,
				(7 - tile.Row) / 8.0 * BoardSprite.ActualHeight) + BoardSprite.Position;
		}

		public override string GetDebugText(MouseEventArgs e)
		{
			List<string> lines = new List<string>();

			Vector2 posAbs = e.GetPositionV();
			lines.Add("Abs coords: " + posAbs.ToString());

			Vector2 posRel = e.GetPosition(BoardSprite.Control);
			lines.Add("Board coords: " + posRel.ToString());

			Tile tile = getTile(posAbs);

			lines.Add("Tile: " + tile.ToStringAlgebraic());

			if (!EnforceTurns)
			{
				lines.Add("Turns Disabled");
			}

			if (Board.IsInCheck(Side.White))
			{
				lines.Add("WHITE IS IN CHECK");
			}
			if (Board.IsInCheck(Side.Black))
			{
				lines.Add("BLACK IS IN CHECK");
			}

			List<Move> allLegalMoves = Board.Validator.GetAllLegalMoves(Board.Turn);
			lines.Add(Board.Turn.ToString() + "'s legal moves: " + allLegalMoves.Count.ToString());

			if (GrabbedPiece != null)
			{
				lines.Add("Grabbed Piece: " + GrabbedPiece.Piece.ToString());

				Vector2 posCenter = GrabbedPiece.RenderedPos + GrabbedPiece.Sprite.ActualSize / 2.0;
				Tile tileCenterOn = getTile(posCenter);

				lines.Add("(would move to " + tileCenterOn.ToString() + ")");

				if (!Board.Validator.IsMovementValid(GrabbedPiece.Piece, tileCenterOn))
				{
					lines.Add("(" + Board.Validator.InvalidErrors.GetOrDefault(tileCenterOn, "INVALID") + ")");
				}
			}

			Piece pointedAtPiece = Board[getTile(posAbs)];

			if (pointedAtPiece != null)
			{
				lines.Add("Piece: " + pointedAtPiece.Side.ToString() + " " + pointedAtPiece.Type.ToString());

				if (!pointedAtPiece.HasMoved)
				{
					lines.Add("Not yet moved");
				}

				if (pointedAtPiece.Type == PieceType.Pawn && pointedAtPiece.PawnJustMovedDouble)
				{
					lines.Add("Open to en passant");
				}

				if (pointedAtPiece.Side != Board.Turn && EnforceTurns)
				{
					lines.Add($"{Board.Turn}'s turn");
				}
			}

			GameStatus status = Board.CheckGameStatus();

			if (ShowHistory)
			{
				lines.Add("");
				string buffer = "";
				int turn = 1;
				foreach (Move m in Board.Moves)
				{
					if (EnforceTurns)
					{
						if (m.Piece.Side == Side.White)
						{
							buffer += m.ToString() + " ";
						}
						else
						{
							buffer += m.ToString();
							lines.Add(turn.ToString() + ". " + buffer);
							buffer = "";
							turn++;
						}
					}
					else
					{
						lines.Add($"{m.Piece.Side}: {m.ToString()}");
					}
				}

				if (EnforceTurns && buffer != "")
				{
					if (status == GameStatus.InProgress)
					{
						lines.Add(turn.ToString() + ". " + buffer + "...");
					}
					else
					{
						lines.Add(turn.ToString() + ". " + buffer + "    ");
					}
				}
			}

			lines.Add("Game Status: " + status.ToString().ToUpper());

			// ---

			string res = "";
			foreach (string l in lines)
			{
				res += l + "\n";
			}

			return res;
		}

		private void onPieceMoved(object sender, PieceMovedEventArgs e)
		{
			// Remove captured pieces
			for (int i = RenderedPieces.Count - 1; i >= 0; i--)
			{
				RenderedPiece rp = RenderedPieces[i];
				if (!Board.Pieces.Contains(rp.Piece))
				{
					rp.Disconnect(Canvas);
					RenderedPieces.RemoveAt(i);
				}
			}

			Board.Validator.ResetCache();
			Board.SwitchTurn();
		}

		public void DeleteAllGhosts()
		{
			for (int i = Canvas.Children.Count - 1; i >= 0; i--)
			{
				UIElement uie = Canvas.Children[i];
				if (uie is Image img)
				{
					if (img.Opacity < 0.5)
					{
						Canvas.Children.RemoveAt(i);
					}
				}
			}
		}
	}
}
