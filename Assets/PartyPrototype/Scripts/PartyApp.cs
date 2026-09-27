using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace PartyPrototype
{
    public sealed class PartyApp : MonoBehaviour
    {
        LanTransport network;
        PartyRules rules;
        State state;
        bool hosting, connecting, joiningForm, solo;
        bool renderPending, forceRender;
        int myId = -1;
        string playerName = "", address = "", notice = "", signature = "";
        Font font;
        RectTransform safeArea, content, portraitFrame;
        Text clock;
        BallTiltGame tiltGame;
        Text tiltStatus;
        ReactionTarget reaction;
        Text reactionStatus;
        int selectedDifficulty;
        int selectedLength = 5;
        int BaseDrinks => (state != null ? state.difficulty : selectedDifficulty) == 2 ? 4 : (state != null ? state.difficulty : selectedDifficulty) == 1 ? 2 : 1;
        InputField nameField, addressField;
        readonly Dictionary<int, double> pending = new Dictionary<int, double>();
        readonly Dictionary<int, double> closing = new Dictionary<int, double>();
        double lastPing;
        static readonly Color Background = new Color32(29, 28, 42, 255);
        static readonly Color Accent = new Color32(22, 169, 159, 255);
        static readonly Color Panel = new Color32(64, 73, 85, 255);
        static readonly Color Cream = new Color32(235, 234, 182, 255);
        static readonly Color Wrong = new Color32(233, 60, 73, 255);
        static readonly Color Correct = new Color32(115, 216, 139, 255);
        static readonly Color[] PlayerColors = { new Color32(22,169,159,255), new Color32(235,234,182,255), new Color32(233,60,73,255), new Color32(126,156,181,255) };
        double Now => Time.realtimeSinceStartupAsDouble;
        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            playerName = PlayerPrefs.GetString("PartyPrototype.Name", "Player");
            address = PlayerPrefs.GetString("PartyPrototype.Address", "192.168.1.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Party Camera", typeof(Camera)).GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Background;
            CreateCanvas(); Render(true);
        }
        void CreateCanvas()
        {
            var go = new GameObject("Party UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280); scaler.matchWidthOrHeight = 1f;
            safeArea = Rect("Safe Area", go.transform); Stretch(safeArea);
            portraitFrame = Rect("Portrait Frame", safeArea);
            portraitFrame.anchorMin = new Vector2(.5f, 0); portraitFrame.anchorMax = new Vector2(.5f, 1);
            portraitFrame.sizeDelta = new Vector2(680, 0);
            var viewport = Rect("Scroll View", portraitFrame);
            Stretch(viewport); viewport.offsetMin = new Vector2(28, 20); viewport.offsetMax = new Vector2(-28, -20);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            scroll.content = content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14; layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("Party Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        static RectTransform Rect(string name, Transform parent)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); return r;
        }
        static void Stretch(RectTransform r)
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Height(GameObject go, float h)
        { var le = go.AddComponent<LayoutElement>(); le.minHeight = h; le.preferredHeight = h; }
        Text Label(string value, int size = 30, Color? color = null)
        {
            var r = Rect("Text", content);
            var text = r.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter; text.text = value; text.color = color ?? Color.white; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
        void Button(string caption, Action action, bool enabled = true, bool primary = false)
        {
            var r = Rect(caption, content); Height(r.gameObject, 76);
            var image = r.gameObject.AddComponent<Image>(); image.color = primary ? Accent : Panel;
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.interactable = enabled;
            button.onClick.AddListener(() => action());
            var t = Rect("Caption", r); Stretch(t); t.offsetMin = new Vector2(16, 4); t.offsetMax = new Vector2(-16, -4);
            var text = t.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = 28; text.text = caption;
            text.supportRichText = false; text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.resizeTextMaxSize = 28;
            text.color = primary ? Background : enabled ? Color.white : Color.gray;
        }
        InputField Input(string value, string placeholder, int limit)
        {
            var r = Rect("Input", content); Height(r.gameObject, 76);
            var image = r.gameObject.AddComponent<Image>(); image.color = Panel;
            var input = r.gameObject.AddComponent<InputField>(); input.targetGraphic = image; input.characterLimit = limit;
            var t = Rect("Value", r); Stretch(t); t.offsetMin = new Vector2(18, 10); t.offsetMax = new Vector2(-18, -10);
            var text = t.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = 30;
            text.color = Color.white; text.supportRichText = false; text.alignment = TextAnchor.MiddleLeft;
            input.textComponent = text;
            var p = Rect("Placeholder", r); Stretch(p); p.offsetMin = t.offsetMin; p.offsetMax = t.offsetMax;
            var hint = p.gameObject.AddComponent<Text>(); hint.font = font; hint.fontSize = 30;
            hint.text = placeholder; hint.color = Color.gray; hint.alignment = TextAnchor.MiddleLeft;
            input.placeholder = hint; input.text = value; return input;
        }
        // Rebuild after the EventSystem has finished processing this frame's input.
        void Render(bool force = false)
        { renderPending = true; forceRender |= force; }
        void LateUpdate()
        {
            if (!renderPending) return;
            bool force = forceRender; renderPending = forceRender = false;
            RenderNow(force);
        }
        void RenderNow(bool force)
        {
            // A player leaving must not recreate another player's running reaction timer.
            if (!force && state != null && state.phase == "Reaction" && reaction != null) return;
            string key = state == null ? notice + connecting : state.phase + state.lengthStep + state.difficulty + state.round + state.activeId + state.selectedIndex + state.result
                + string.Join("|", state.players.Select(p => p.id + ":" + p.name + ":" + (state.phase == "Reaction" || state.phase == "Tilt" ? 0 : p.gulps)));
            if (!force && signature == key) { if (clock != null && state != null) clock.text = state.seconds + " seconds"; return; }
            if (state == null || state.phase != "Tilt")
            { if (tiltGame != null) Destroy(tiltGame.gameObject); tiltGame = null; }
            signature = key; clock = null; tiltStatus = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            foreach (Transform child in content)
            {
                foreach (var graphic in child.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            content.anchoredPosition = Vector2.zero;
            Label("HinkBuddy", 30, Accent);
            if (state != null && state.fullGame && state.phase != "Finished") Label(state.round == state.totalRounds ? "FINAL ROUND · BALL TILT" : "ROUND " + state.round + " / " + state.totalRounds, 22, Cream);
            if (state == null)
            {
                Space(65);
                Label(joiningForm ? "JOIN THE PARTY" : "GET THE PARTY STARTED", 38, Cream);
                Label(joiningForm ? "Enter the address shown on the host’s screen." : "One host. One Wi-Fi. Everyone plays.", 24, Color.white);
                Space(22);
                DifficultyPicker(false);
                if (!joiningForm) LengthPicker(false);
                Label("YOUR NAME", 20, Accent); nameField = Input(playerName, "Enter your name", 20);
                addressField = null;
                if (joiningForm)
                {
                    Label("HOST ADDRESS", 20, Accent); addressField = Input(address, "192.168.1.100", 64);
                    Space(12);
                    Button(connecting ? "Connecting…" : "Join party", Join, !connecting, true);
                    Button(connecting ? "Cancel" : "Back", () => { if (nameField != null) playerName = nameField.text; joiningForm = false; Disconnect(""); });
                }
                else
                {
                    Space(12);
                    Label("PLAY ON YOUR OWN", 22, Accent);
                    Button("Solo full game", () => StartSolo(false, false, true), true, true);
                    Button("Solo quiz", () => StartSolo(false), true, true);
                    Button("Solo Ball Tilt", () => StartSolo(true));
                    Button("Solo reaction test", () => StartSolo(false, true));
                    Label("PLAY WITH FRIENDS", 22, Accent);
                    Button("Host a party", Host, true, true);
                    Button("Join a party", () => { playerName = nameField.text; joiningForm = true; Render(true); });
                }
                if (!string.IsNullOrEmpty(notice)) Label(notice, 24, Cream);
                Space(30);
                Label("2–8 PLAYERS  /  SAME WI-FI", 20, Accent);
                var build = Resources.Load<TextAsset>("PartyBuildInfo");
                Label("HUD 0.8.1  ·  " + (build != null ? build.text.Trim() : "Editor / source"), 17, Color.gray);
                return;
            }
            string active = state.players.FirstOrDefault(p => p.id == state.activeId)?.name ?? "Player";
            if (state.phase == "Lobby")
            {
                Space(22);
                Label("PARTY LOBBY", 38, Cream);
                DifficultyPicker(true);
                LengthPicker(true);
                Label(state.players.Count + " / 8 PLAYERS CONNECTED", 24, Accent);
                Space(8);
                foreach (var p in state.players)
                {
                    string role = p.id == 0 ? "HOST" : "JOINED";
                    Card(p.name + (p.id == myId ? " (you)" : "") + "  ·  " + role, 64, Panel, Cream, 27);
                }
                Space(12);
                Label(hosting ? "INVITE YOUR FRIENDS" : "YOU’RE CONNECTED", 23, Accent);
                if (hosting)
                {
                    Label("Same Wi-Fi → Join a party → enter the host address", 23, Cream);
                    Card(Addresses(), 105, Panel, Color.white, 30);
                    Label("If several addresses appear, use your Wi-Fi address.", 19, Color.gray);
                }
                else Card("Host address: " + address, 72, Panel, Cream, 27);
                Space(12);
                Label(state.players.Count < 2 ? "Waiting for at least one more player" : "Ready · " + state.players.Count + " players can start", 25, Cream);
                if (hosting) { Button("Start full game  >", () => Command("fullStart"), state.players.Count >= 2, true); Button("Quiz-only test", () => Command("start"), state.players.Count >= 2); }
                else Card("Waiting for the host to start", 76, Background, Cream, 25);
                if (hosting) { Button("Test Ball Tilt", () => Command("tiltStart"), state.players.Count >= 2); Button("Test reaction", () => Command("reactionStart"), state.players.Count >= 2); }
                Label("Quiz + Reaction + Ball Tilt · cumulative scores", 21, Color.gray);
            }
            else if (state.phase == "Tilt")
            {
                Label("BALL TILT", 36, Cream);
                Label("Tilt to roll the ball to the green finish.", 23, Accent);
                if (tiltGame == null)
                {
                    var game = new GameObject("Ball Tilt Round"); game.transform.SetParent(transform);
                    tiltGame = game.AddComponent<BallTiltGame>(); tiltGame.Initialize();
                    tiltGame.Finished = () => Command("tiltFinish");
                    tiltGame.Failed = () => Command("tiltFail");
                }
                var view = Rect("Course View", content); Height(view.gameObject, 570);
                var picture = Rect("Portrait Camera Image", view);
                var fit = picture.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 640f / 800f;
                var preview = picture.gameObject.AddComponent<RawImage>(); preview.texture = tiltGame.View; preview.raycastTarget = false;
                tiltStatus = Label("Get ready…", 25, Cream);
                Label(Application.isMobilePlatform ? "Hold comfortably, then calibrate. Tilt gently." : "WASD / arrows steer the ball", 22, Accent);
                Button("Calibrate neutral tilt", () => tiltGame.Calibrate());
            }
            else if (state.phase == "Reaction")
            {
                Label("REACTION TEST", 36, Cream);
                Label("Wait for green. Tap the circle, not the background.", 23, Accent);
                var area = Rect("Reaction Area", content); Height(area.gameObject, 520);
                area.gameObject.AddComponent<Image>().color = Panel;
                var target = Rect("Moving Circle", area); target.anchorMin = target.anchorMax = new Vector2(.5f,.5f);
                target.sizeDelta = new Vector2(110,110);
                reaction = target.gameObject.AddComponent<ReactionTarget>(); reaction.Initialize(state.reactionSeed);
                reaction.Result = ms => Command("reactionResult", ms);
                reactionStatus = Label("Get ready…", 26, Cream);
            }
            else if (state.phase == "Intro")
            {
                Space(100);
                Label(myId == state.activeId ? "YOU’RE UP NEXT" : "UP NEXT", 26, Cream);
                PlayerPopup(state.activeId, active, "", false);
                Label(myId == state.activeId ? "Get ready to answer" : "Get ready — it’s " + active + "’s turn", 25, Cream);
            }
            else if (state.phase == "Drink")
            {
                Space(75);
                Label("THIS ROUND", 24, Accent);
                PlayerPopup(state.drinkId, state.drinkName, "DRINKS " + state.drinkAmount + (state.drinkAmount == 1 ? " GULP" : " GULPS"), true);
                Space(25);
                Continue("Next  >");
            }
            else if (state.phase == "Scoreboard" || state.phase == "Finished")
            {
                Label(state.phase == "Finished" ? "FINAL SCORES" : "SCOREBOARD", 42, Cream);
                Label("TOTAL GULPS  ·  FEWEST WINS", 23, Accent);
                if (!string.IsNullOrEmpty(state.result)) Label(state.result, 25, Cream);
                if (state.mode == "Reaction")
                    foreach (var p in state.players.OrderBy(p => p.failed ? int.MaxValue : p.reactionMs))
                        Label(p.name + " · " + (p.failed ? "Early / no valid tap" : PartyRules.ReactionSeconds(p.reactionMs)), 24, Cream);
                int rank = 0;
                foreach (var p in state.players.OrderBy(p => p.gulps))
                    Card((++rank) + ".  " + p.name + (p.id == myId ? " (you)" : "") + "     " + p.gulps
                        + (p.id == state.drinkId ? "  (+" + state.drinkAmount + ")" : ""), 86, PlayerColor(p.id), Background, 30);
                if (state.phase == "Finished")
                {
                    Label(state.result, 26);
                    if (solo)
                    {
                        Button("Play full game again", () => StartSolo(false, false, true), true, true);
                        Button("Play quiz again", () => StartSolo(false), true, true);
                        Button("Play Ball Tilt again", () => StartSolo(true));
                        Button("Play reaction again", () => StartSolo(false, true));
                    }
                    else if (hosting) Button("Back to lobby", () => Command("reset"), true, true);
                    else Label("Waiting for the host…", 26);
                }
                else Continue(state.round >= state.totalRounds ? "Finish game" : "Next round  >");
            }
            else if (state.phase == "Choose")
            {
                Label("GIVE A GULP", 40, Cream);
                Label(active + " answered correctly.", 30);
                if (myId == state.activeId)
                    foreach (var p in state.players.Where(p => p.id != myId))
                    { int target = p.id; Button("Give " + BaseDrinks + " drink(s) to " + p.name, () => Command("give", target)); }
                else Label("Waiting for " + active + " to choose someone…", 28, Accent);
            }
            else if (state.phase == "Quiz" || state.phase == "Reveal")
            {
                Label("ROUND " + state.round + " / " + state.totalRounds + "  ·  " + active, 25, Accent);
                if (state.phase == "Quiz") clock = Label(state.seconds + " seconds", 32, Cream);
                else Label(state.result, 32, state.selectedIndex == state.correctIndex ? Correct : Wrong);
                Card(state.question, 210, Cream, Background, 34);
                for (int i = 0; i < state.answers.Length; i++) AnswerCard(i);
                if (state.phase == "Reveal") Continue(state.drinkId < 0 && !solo ? "Choose who drinks  >" : "Continue  >");
                else Label(myId == state.activeId ? "YOUR TURN · TAP AN ANSWER" : "Watching " + active + " answer…", 24, Cream);
            }
            Space(16);
            Button(solo ? "Back to menu" : hosting ? "Leave / end session" : "Leave session", () => Disconnect("Session ended."));
            var leave = content.GetChild(content.childCount - 1).GetComponent<LayoutElement>();
            leave.minHeight = leave.preferredHeight = 36;
            leave.GetComponent<Image>().color = Background;
            var leaveText = leave.GetComponentInChildren<Text>();
            leaveText.resizeTextMaxSize = 20; leaveText.color = Color.gray;
        }
        void LengthPicker(bool lobby)
        {
            int step = lobby ? state.lengthStep : selectedLength;
            int players = lobby ? state.players.Count : 1;
            Label("FULL GAME · LENGTH " + step + " / 10", 22, Accent);
            Label(PartyRules.OrdinaryRounds(players, step) + " rounds + Ball Tilt finale", 23, Cream);
            if (!lobby || hosting)
            {
                Button("Shorter game", () => ChangeLength(Math.Max(1, step - 1), lobby), step > 1);
                Button("Longer game", () => ChangeLength(Math.Min(10, step + 1), lobby), step < 10);
            }
        }
        void ChangeLength(int step, bool lobby)
        {
            selectedLength = step;
            if (lobby) Command("length", step);
            else
            {
                if (nameField != null) playerName = nameField.text;
                if (addressField != null) address = addressField.text;
                Render(true);
            }
        }
        void DifficultyPicker(bool lobby)
        {
            int selected = lobby ? state.difficulty : selectedDifficulty;
            Label("DIFFICULTY · " + PartyRules.DifficultyNames[selected].ToUpperInvariant(), 22, Accent);
            if (!lobby || hosting)
            {
                Button("Change difficulty", () => {
                    if (nameField != null && state == null) playerName = nameField.text;
                    if (addressField != null && state == null) address = addressField.text;
                    selectedDifficulty = (selected + 1) % 3;
                    if (lobby) Command("difficulty", selectedDifficulty); else Render(true);
                });
            }
            Label("Base: " + BaseDrinks + " drinks  ·  Maximum: " + (BaseDrinks + 2), 21, Cream);
        }
        Color PlayerColor(int id) => PlayerColors[Math.Abs(id) % PlayerColors.Length];
        void Continue(string caption)
        {
            if (hosting) Button(caption, () => Command("next"), true, true);
            else Card("Waiting for the host to continue…", 76, Background, Cream, 24, Background);
        }
        void Space(float height) { Height(Rect("Space", content).gameObject, height); }
        void PlayerPopup(int id, string name, string detail, bool drink)
        {
            // One grouped identity: circular portrait connected to a single nameplate.
            var root = Rect("Player Popup", content); Height(root.gameObject, drink ? 390 : 310);
            var avatar = Rect("Avatar", root);
            avatar.anchorMin = avatar.anchorMax = new Vector2(.5f, 1);
            avatar.pivot = new Vector2(.5f, 1); avatar.sizeDelta = new Vector2(204, 204);
            avatar.gameObject.AddComponent<PartyAvatar>().color = PlayerColor(id);
            var plate = Rect("Nameplate", root);
            plate.anchorMin = new Vector2(.08f, 1); plate.anchorMax = new Vector2(.92f, 1);
            plate.pivot = new Vector2(.5f, 1); plate.anchoredPosition = new Vector2(0, -198); plate.sizeDelta = new Vector2(0, 68);
            plate.gameObject.AddComponent<Image>().color = PlayerColor(id);
            PopupText(plate, name, Background, 34);
            if (drink)
            {
                var amount = Rect("Drink Amount", root);
                amount.anchorMin = new Vector2(0, 1); amount.anchorMax = new Vector2(1, 1);
                amount.pivot = new Vector2(.5f, 1); amount.anchoredPosition = new Vector2(0, -290); amount.sizeDelta = new Vector2(0, 76);
                PopupText(amount, detail, Cream, 42);
            }
        }
        void PopupText(RectTransform parent, string value, Color ink, int size)
        {
            var rect = Rect("Caption", parent); Stretch(rect);
            rect.offsetMin = new Vector2(12, 4); rect.offsetMax = new Vector2(-12, -4);
            var t = rect.gameObject.AddComponent<Text>(); t.font = font; t.text = value; t.color = ink;
            t.fontSize = size; t.resizeTextForBestFit = true; t.resizeTextMinSize = 18; t.resizeTextMaxSize = size;
            t.alignment = TextAnchor.MiddleCenter; t.supportRichText = false; t.raycastTarget = false;
        }
        RectTransform Card(string value, float height, Color fill, Color ink, int size, Color? border = null)
        {
            var r = Rect("Card", content); Height(r.gameObject, height);
            var image = r.gameObject.AddComponent<Image>(); image.color = fill;
            var outline = r.gameObject.AddComponent<Outline>(); outline.effectColor = border ?? Accent;
            outline.effectDistance = new Vector2(3, -3);
            var t = Rect("Text", r); Stretch(t); t.offsetMin = new Vector2(20, 10); t.offsetMax = new Vector2(-20, -10);
            var text = t.gameObject.AddComponent<Text>(); text.font = font; text.fontSize = size; text.text = value;
            text.supportRichText = false; text.color = ink; text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.resizeTextMaxSize = size;
            text.raycastTarget = false;
            return r;
        }
        void AnswerCard(int index)
        {
            bool reveal = state.phase == "Reveal", selected = reveal && index == state.selectedIndex;
            bool correct = reveal && index == state.correctIndex;
            Color edge = reveal ? (correct ? Correct : Wrong) : Accent;
            string tag = reveal ? (correct ? "  ·  CORRECT" : "  ·  WRONG") : "";
            if (selected) tag += "  ·  CHOSEN";
            var r = Card(((char)('A' + index)) + "   " + state.answers[index] + tag, 100,
                selected ? edge : Panel, selected ? Background : Color.white, 28, edge);
            r.GetComponent<Outline>().effectDistance = new Vector2(selected ? 6 : 2, selected ? -6 : -2);
            var button = r.gameObject.AddComponent<Button>(); button.targetGraphic = r.GetComponent<Image>();
            button.transition = Selectable.Transition.None;
            button.interactable = !reveal && myId == state.activeId;
            button.onClick.AddListener(() => Command("answer", index));
        }
        void Roster()
        { Label(string.Join("\n", state.players.Select(p => p.name + (p.id == myId ? " (you)" : "") + "  ·  " + p.gulps + " gulps")), 27); }
        static string Addresses()
        {
            try
            {
                var ips = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address)
                    .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    .Select(ip => ip.ToString()).Distinct().ToArray();
                return ips.Length > 0 ? string.Join("\n", ips) : "Check this device’s Wi-Fi settings for its IPv4 address.";
            }
            catch { return "Check this device’s Wi-Fi settings for its IPv4 address."; }
        }
        bool SaveFields()
        {
            playerName = nameField.text.Trim(); address = addressField != null ? addressField.text.Trim() : address;
            if (playerName.Length == 0) { notice = "Enter a name first."; Render(true); return false; }
            PlayerPrefs.SetString("PartyPrototype.Name", playerName); PlayerPrefs.SetString("PartyPrototype.Address", address);
            return true;
        }
        void StartSolo(bool ballTilt, bool reactionTest = false, bool full = false)
        {
            if (state == null && !SaveFields()) return;
            try
            {
                network?.Dispose(); network = null;
                var bank = JsonUtility.FromJson<QuestionBank>(Resources.Load<TextAsset>("PartyQuestions").text);
                rules = new PartyRules(bank.questions, Environment.TickCount, true);
                rules.Join(0, playerName); rules.SetDifficulty(selectedDifficulty); rules.SetLength(selectedLength);
                solo = hosting = true; connecting = false; myId = 0;
                if (full) rules.StartFull(Now); else if (reactionTest) rules.StartReaction(Now); else if (ballTilt) rules.StartTilt(Now); else rules.Start(Now);
                state = rules.State; Render(true);
            }
            catch (Exception e) { Disconnect("Could not start solo: " + e.Message); }
        }
        void Host()
        {
            if (!SaveFields()) return;
            try
            {
                var bank = JsonUtility.FromJson<QuestionBank>(Resources.Load<TextAsset>("PartyQuestions").text);
                rules = new PartyRules(bank.questions, Environment.TickCount); rules.Join(0, playerName); rules.SetDifficulty(selectedDifficulty); rules.SetLength(selectedLength);
                network = new LanTransport(); network.Host(); hosting = true; myId = 0; state = rules.State;
                Render(true);
            }
            catch (Exception e) { Disconnect("Could not host: " + e.Message); }
        }
        void Join()
        {
            if (!SaveFields()) return;
            if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            { notice = "Enter the host’s IPv4 address, such as 192.168.1.100."; Render(true); return; }
            network = new LanTransport(); connecting = true; notice = ""; network.Connect(address); Render(true);
        }
        void Command(string type, int value = 0)
        {
            var message = new Message { type = type, value = value, round = state.round };
            if (hosting) HandleCommand(0, message);
            else network.Send(0, JsonUtility.ToJson(message));
        }
        void HandleCommand(int id, Message m)
        {
            bool changed = false;
            if (m.type == "reactionResult") changed = rules.ReactionResult(id, m.round, m.value, Now);
            else if (m.type == "tiltFail") changed = rules.FailTilt(id, m.round, Now);
            else if (m.type == "tiltFinish") changed = rules.FinishTilt(id, m.round, Now);
            else if (m.type == "answer") changed = rules.Answer(id, m.round, m.value, Now);
            else if (m.type == "give") changed = rules.Give(id, m.round, m.value);
            else if (id == 0)
            {
                if (m.type == "length") changed = rules.SetLength(m.value);
                if (m.type == "fullStart") changed = rules.StartFull(Now);
                if (m.type == "difficulty") changed = rules.SetDifficulty(m.value);
                if (m.type == "reactionStart") changed = rules.StartReaction(Now);
                if (m.type == "tiltStart") changed = rules.StartTilt(Now);
                if (m.type == "start") changed = rules.Start(Now);
                if (m.type == "next") changed = rules.Next(Now);
                if (m.type == "reset") changed = rules.Reset();
            }
            if (changed) Publish();
        }
        void Publish()
        {
            state = rules.State;
            string json = JsonUtility.ToJson(new Message { type = "state", state = state });
            foreach (var p in state.players) if (p.id != 0) network.Send(p.id, json);
            Render();
        }
        void Update()
        {
            if (tiltGame != null && state != null && state.phase == "Tilt")
            {
                tiltGame.Running = state.seconds <= 60;
                var mine = state.players.FirstOrDefault(p => p.id == myId);
                if (tiltStatus != null) tiltStatus.text = state.seconds > 60 ? "Starting in " + (state.seconds - 60)
                    : mine != null && mine.failed ? "Out — maximum penalty. Waiting for the others…"
                    : mine != null && mine.tiltSeconds >= 0 ? "Finished! Waiting for the others…"
                    : state.seconds + " seconds";
            }
            if (reaction != null && state != null && state.phase == "Reaction")
            {
                reaction.Running = state.seconds <= 20;
                if (reactionStatus != null) reactionStatus.text = state.seconds > 20 ? "Starting in " + (state.seconds - 20) : reaction.Status;
            }
            var safe = Screen.safeArea;
            safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            portraitFrame.sizeDelta = new Vector2(Mathf.Min(680, safeArea.rect.width), 0);
            if (solo)
            {
                if (rules.Tick(Now)) Publish();
                return;
            }
            if (network == null) return;
            int budget = 64;
            while (network != null && budget-- > 0 && network.Poll(out var evt))
            {
                if (evt.kind == "error") { Disconnect(evt.data); break; }
                if (evt.kind == "connected")
                {
                    if (hosting) pending[evt.peer] = Now + 8;
                    else network.Send(0, JsonUtility.ToJson(new Message { type = "hello", name = playerName }));
                }
                else if (evt.kind == "disconnected")
                {
                    if (!hosting) { Disconnect("Host disconnected. Rejoin a new lobby to play again."); break; }
                    pending.Remove(evt.peer); closing.Remove(evt.peer);
                    if (rules.Leave(evt.peer)) Publish();
                }
                else if (evt.kind == "message")
                {
                    try
                    {
                        var m = JsonUtility.FromJson<Message>(evt.data);
                        if (m == null) continue;
                        if (m.type == "ping") continue;
                        if (hosting)
                        {
                            if (m.type == "hello" && pending.Remove(evt.peer))
                            {
                                string error = rules.Join(evt.peer, m.name);
                                if (error != null)
                                { network.Send(evt.peer, JsonUtility.ToJson(new Message { type = "reject", error = error })); closing[evt.peer] = Now + 1; }
                                else
                                { network.Send(evt.peer, JsonUtility.ToJson(new Message { type = "welcome", yourId = evt.peer, state = rules.State })); Publish(); }
                            }
                            else if (rules.State.players.Any(p => p.id == evt.peer)) HandleCommand(evt.peer, m);
                        }
                        else if (m.type == "reject") Disconnect(m.error);
                        else if (m.type == "welcome" || m.type == "state")
                        {
                            if (m.type == "welcome") myId = m.yourId;
                            if (m.state == null) continue;
                            state = m.state; connecting = false; Render();
                        }
                    }
                    catch (ArgumentException) { network?.Drop(evt.peer); }
                }
            }
            if (network == null) return;
            if (hosting)
            {
                foreach (int id in pending.Where(p => p.Value < Now).Select(p => p.Key).ToArray()) { pending.Remove(id); network.Drop(id); }
                foreach (int id in closing.Where(p => p.Value < Now).Select(p => p.Key).ToArray()) { closing.Remove(id); network.Drop(id); }
                if (rules.Tick(Now)) Publish();
            }
            if (Now - lastPing > 2)
            {
                lastPing = Now;
                if (hosting) { foreach (var p in state.players) if (p.id != 0) network.Send(p.id, "{\"type\":\"ping\"}"); }
                else network.Send(0, "{\"type\":\"ping\"}");
            }
        }
        void Disconnect(string message)
        {
            network?.Dispose(); network = null; state = null; rules = null;
            hosting = connecting = solo = false; myId = -1; pending.Clear(); closing.Clear(); notice = message;
            Render(true);
        }
        void OnDestroy() { network?.Dispose(); Screen.sleepTimeout = SleepTimeout.SystemSetting; }
    }
}
