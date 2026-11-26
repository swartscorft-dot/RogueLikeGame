using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// What she cannot afford, made plain (the menus' help, <see cref="MenuHelpValues"/>; a friend, 2026-11-25: "kanskje en feedback
    /// når man prøver å kjøpe en upgrade man ikke har råd til? Bare polish, men føler det er vanlig"), as part of
    /// <see cref="RunHud"/>, wherever something is bought with Echoes: the start menu's UPGRADES and the screen between runs, whose
    /// cards are the same (<c>DrawCard</c>). A card she cannot afford is drawn under a dark veil before she tries; a try at it
    /// (a click, Enter, the start menu's press: every way ends in the rack's, the parts' or the ranks' own press, which this is told
    /// of first) flashes its price and the Echoes plate red with one dying shake, sounds the placeholder sounds' dull tick, and
    /// says how many more are needed. The press itself is the screen's own as before: nothing is bought, no price changes.
    /// </summary>
    public sealed partial class RunHud
    {
        readonly List<int> _cardNeed = new List<int>(); // a card's Echoes still needed, 0 when she can pay it (or it is not for sale, owned, at its last rank)
        int _refusedCard = -1;
        float _refusedAt = -100f;
        string _refusedLine = "";

        /// <summary>How many tries at something she could not afford were answered (for tests).</summary>
        public int EchoesRefusals { get; private set; }
        /// <summary>The card of the last refused try (among <see cref="BetweenCardNames"/>), or -1.</summary>
        public int EchoesRefusedCard => _refusedCard;
        /// <summary>Seconds since the last refused try, or -1 when there was none.</summary>
        public float EchoesRefusalAge => _refusedCard >= 0 ? Time.unscaledTime - _refusedAt : -1f;
        /// <summary>The last refused try's line ("Not enough Echoes: 25 more needed for Second Look."), or empty.</summary>
        public string EchoesRefusalLine => _refusedLine;

        /// <summary>The refusal's line stands on the screen between runs this long after the try, in place of the screen's own note.</summary>
        const float RefusalLineSeconds = 4f;

        /// <summary>Each card's Echoes still needed, worked out with the cards (LayOutBetween).</summary>
        void MakeCardNeeds()
        {
            _cardNeed.Clear();
            for (int i = 0; i < _cardName.Count; i++) _cardNeed.Add(CardNeed(i));
        }

        /// <summary>The Echoes she still needs for card <paramref name="card"/>: 0 when she can pay it or it cannot be bought for Echoes now.</summary>
        int CardNeed(int card)
        {
            if (_levels == null || card < 0) return 0;
            MetaState s = _levels.BetweenRunsState;
            MetaValues mv = _levels.MetaValues;
            if (card < _rackCards)
            {
                MetaPiece[] pieces = Pieces;
                if (card >= pieces.Length) return 0;
                MetaPiece p = pieces[card];
                return MetaRack.CanBuy(s, mv, p.Id) == MetaBuy.NotEnough ? MenuHelpRules.Need(p.Price, s.Currency) : 0;
            }
            if (card < _weaponEnd)
            {
                int k = card - _rackCards;
                List<WeaponAttachment> parts = WeaponRack.InSlotOrder(mv); // the order the parts' press reads them in (PickWeaponLine)
                if (k >= parts.Count || parts[k] == null) return 0;
                WeaponAttachment w = parts[k];
                return WeaponRack.Owns(s, w.Id) ? 0 : MenuHelpRules.Need(w.Price, s.Currency);
            }
            MetaRank[] ranks = RankTable;
            int r = card - _weaponEnd;
            if (r >= ranks.Length || ranks[r] == null || MetaRanks.AtMost(s, ranks[r])) return 0;
            return MenuHelpRules.Need(MetaRanks.NextPrice(s, mv, ranks[r]), s.Currency);
        }

        /// <summary>A card she cannot afford now, drawn dimmer (with the help on). For tests.</summary>
        public bool BetweenCardUnaffordable(int card)
        {
            LayOutBetween();
            return MenuHelpValues.On && card >= 0 && card < _cardNeed.Count && _cardNeed[card] > 0;
        }

        /// <summary>An item of the start menu's UPGRADES she cannot afford now. For tests.</summary>
        public bool MenuItemUnaffordable(StartMenu menu, int item)
        {
            EnsureMenuItems(menu);
            return item >= 0 && item < _menuCards.Count && BetweenCardUnaffordable(_menuCards[item]);
        }

        /// <summary>
        /// A press is about to try card <paramref name="card"/> (the rack's, the parts' and the ranks' own presses tell this first):
        /// when she cannot afford it, the refusal is answered. The press goes on as before either way.
        /// </summary>
        void NoteEchoesTry(int card)
        {
            if (!MenuHelpValues.On || _levels == null) return;
            LayOutBetween();
            int need = CardNeed(card);
            if (need <= 0 || card >= _cardName.Count) return;
            _refusedCard = card;
            _refusedAt = Time.unscaledTime;
            _refusedLine = MenuHelpRules.NotEnough(need, _levels.MetaValues.CurrencyName, _cardName[card]);
            EchoesRefusals++;
            if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(PlaceholderSound.OffTick, MenuHelpValues.RefusalSoundGain);
        }

        /// <summary>The rack's, the parts' and the ranks' presses tell of a try first: piece <paramref name="piece"/> of the rack.</summary>
        void NoteRackTry(int piece)
        {
            if (!MenuHelpValues.On) return;
            LayOutBetween();
            NoteEchoesTry(piece);
        }

        /// <summary>A part of her pistols (its row among the parts).</summary>
        void NotePartTry(int part)
        {
            if (!MenuHelpValues.On) return;
            LayOutBetween();
            NoteEchoesTry(_rackCards + part);
        }

        /// <summary>A rank (its row among the ranks).</summary>
        void NoteRankTry(int rank)
        {
            if (!MenuHelpValues.On) return;
            LayOutBetween();
            NoteEchoesTry(_weaponEnd + rank);
        }

        /// <summary>The refusal's line for the start menu's foot when <paramref name="card"/> was just refused; null otherwise.</summary>
        string RefusalFor(int card) => MenuHelpValues.On && card == _refusedCard && EchoesRefusalAge >= 0f && EchoesRefusalAge < RefusalLineSeconds ? _refusedLine : null;

        /// <summary>The screen between runs' note: the refusal's line for a while after the try, else the screen's own.</summary>
        string BetweenNoteNow(string own) => MenuHelpValues.On && _refusedCard >= 0 && EchoesRefusalAge < RefusalLineSeconds ? _refusedLine : own;

        /// <summary>A note's colour: the refusal's line red (the help on), every other note teal as before.</summary>
        Color NoteInk(string note, InterfaceValues v) =>
            MenuHelpValues.On && _refusedLine.Length > 0 && ReferenceEquals(note, _refusedLine) ? InterfaceStyle.C(v.Red) : InterfaceStyle.C(v.Teal);

        /// <summary>The refusal's shake (layout pixels) and flash (0 to 1) now, for card <paramref name="card"/> (-1: the Echoes plate).</summary>
        void RefusalMotion(int card, out float shake, out float flash)
        {
            shake = flash = 0f;
            if (!MenuHelpValues.On || _refusedCard < 0 || (card >= 0 && card != _refusedCard)) return;
            float age = EchoesRefusalAge;
            shake = MenuHelpRules.Shake(age);
            flash = MenuHelpRules.Flash(age);
        }

        /// <summary>The Echoes plate's shake now (layout pixels) and its flash (0 to 1), for the start menu's plate.</summary>
        public float EchoesShakeNow(out float flash)
        {
            RefusalMotion(-1, out float shake, out flash);
            return shake;
        }

        /// <summary>The dark veil over a card she cannot afford (drawn after the card).</summary>
        void VeilUnaffordable(int card, Rect r, float pivot, float slant, InterfaceValues v)
        {
            if (!MenuHelpValues.On || card < 0 || card >= _cardNeed.Count || _cardNeed[card] <= 0) return;
            RefusalMotion(card, out float _, out float flash); // lifted while its refusal flashes, so the red price reads
            InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, MenuHelpValues.UnaffordableVeil * (1f - flash)));
        }
    }
}
