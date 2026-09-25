using System;
using System.Collections.Generic;
using System.Linq;

namespace PartyPrototype
{
    [Serializable] public class Question { public string question; public string[] answers; public int correctAnswerIndex; }
    [Serializable] public class QuestionBank { public Question[] questions; }
    [Serializable] public class Player { public int id; public string name; public int gulps; public float tiltSeconds = -1; }
    [Serializable] public class State
    {
        public string phase = "Lobby";
        public int round, activeId, seconds, correctIndex = -1, selectedIndex = -1, drinkId = -1, drinkAmount;
        public string drinkName = "";
        public string question = "", result = "";
        public string[] answers = new string[0];
        public List<Player> players = new List<Player>();
    }
    [Serializable] public class Message
    {
        public string type, name, error;
        public int round, value, yourId;
        public State state;
    }

    // Only the host owns Rules. Clients send intentions, never scores or game state.
    public sealed class PartyRules
    {
        public const int MaxPlayers = 8, RoundCount = 10;
        public readonly State State = new State();
        readonly Question[] bank;
        readonly Random random;
        readonly Queue<int> deck = new Queue<int>();
        double deadline;
        int answer;
        int turnIndex = -1;
        bool tiltOnly;
        double tiltStart;

        public PartyRules(Question[] questions, int seed)
        {
            bank = questions.Where(q => q != null && !string.IsNullOrWhiteSpace(q.question)
                && q.answers != null && q.answers.Length == 4 && q.answers.All(a => !string.IsNullOrWhiteSpace(a))
                && q.correctAnswerIndex >= 0 && q.correctAnswerIndex < 4).ToArray();
            if (bank.Length == 0) throw new ArgumentException("The question bank has no valid questions.");
            random = new Random(seed);
        }
        public string Join(int id, string name)
        {
            if (State.phase != "Lobby") return "The game has started. Join the next lobby.";
            if (State.players.Count >= MaxPlayers) return "This lobby is full.";
            if (State.players.Any(p => p.id == id)) return "Already joined.";
            name = new string((name ?? "").Where(c => !char.IsControl(c)).Take(20).ToArray()).Trim();
            if (name.Length == 0) return "Enter a player name.";
            if (State.players.Any(p => string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase)))
                return "That name is taken. Choose another.";
            State.players.Add(new Player { id = id, name = name });
            return null;
        }
        public bool Leave(int id)
        {
            int index = State.players.FindIndex(p => p.id == id);
            if (index < 0) return false;
            string name = State.players[index].name;
            State.players.RemoveAt(index);
            if (index <= turnIndex) turnIndex--;
            if (State.phase != "Lobby" && State.phase != "Finished")
            {
                if (State.players.Count < 2)
                { State.phase = "Finished"; State.result = "Not enough players. Return to the lobby to invite someone."; }
                else if (State.activeId == id && (State.phase == "Intro" || State.phase == "Quiz" || State.phase == "Reveal" || State.phase == "Choose"))
                { State.phase = "Scoreboard"; State.result = name + " disconnected. This turn was skipped."; State.correctIndex = answer; }
            }
            return true;
        }
        public bool Start(double now)
        {
            if (State.phase != "Lobby" || State.players.Count < 2) return false;
            State.round = 0; turnIndex = -1; tiltOnly = false;
            foreach (var p in State.players) p.gulps = 0;
            BeginTurn(now); return true;
        }
        void BeginTurn(double now)
        {
            if (deck.Count == 0)
            {
                var order = Enumerable.Range(0, bank.Length).ToArray();
                for (int i = order.Length - 1; i > 0; i--)
                { int j = random.Next(i + 1); int temp = order[i]; order[i] = order[j]; order[j] = temp; }
                foreach (int i in order) deck.Enqueue(i);
            }
            var q = bank[deck.Dequeue()];
            turnIndex = (turnIndex + 1) % State.players.Count;
            State.activeId = State.players[turnIndex].id;
            State.round++; State.phase = "Intro"; State.result = "";
            State.question = q.question; State.answers = (string[])q.answers.Clone();
            State.correctIndex = -1; State.selectedIndex = -1; State.drinkId = -1; State.drinkAmount = 0; State.drinkName = ""; answer = q.correctAnswerIndex;
            deadline = now + 2.5; State.seconds = 15;
        }
        public bool Tick(double now)
        {
            if (State.phase == "Tilt")
            {
                int remaining = Math.Max(0, (int)Math.Ceiling(deadline - now));
                if (remaining == 0) { EndTilt(); return true; }
                if (remaining != State.seconds) { State.seconds = remaining; return true; }
                return false;
            }
            if (State.phase == "Intro" && now >= deadline)
            { State.phase = "Quiz"; deadline = now + 15; State.seconds = 15; return true; }
            if (State.phase != "Quiz") return false;
            int seconds = Math.Max(0, (int)Math.Ceiling(deadline - now));
            if (seconds == 0)
            { PenalizeActive("Time is up!"); return true; }
            if (State.seconds == seconds) return false;
            State.seconds = seconds; return true;
        }
        public bool Answer(int id, int round, int choice, double now)
        {
            if (State.phase != "Quiz" || id != State.activeId || round != State.round || choice < 0 || choice > 3) return false;
            if (now >= deadline) { PenalizeActive("Time is up!"); return true; }
            State.correctIndex = answer; State.selectedIndex = choice;
            if (choice != answer) PenalizeActive("Wrong answer.");
            else { State.phase = "Reveal"; State.result = "Correct answer!"; }
            return true;
        }
        void PenalizeActive(string reason)
        {
            var p = State.players.First(x => x.id == State.activeId);
            p.gulps++; State.correctIndex = answer; State.seconds = 0;
            State.phase = "Reveal"; State.drinkId = p.id; State.drinkName = p.name; State.drinkAmount = 1; State.result = reason;
        }
        public bool Give(int id, int round, int target)
        {
            if (State.phase != "Choose" || id != State.activeId || round != State.round || target == id) return false;
            var p = State.players.FirstOrDefault(x => x.id == target);
            if (p == null) return false;
            p.gulps++; State.drinkId = p.id; State.drinkName = p.name; State.drinkAmount = 1; State.phase = "Drink"; State.result = p.name + " drinks 1 gulp."; return true;
        }
        public bool Next(double now)
        {
            if (State.phase == "Reveal")
            { State.phase = State.drinkId < 0 ? "Choose" : "Drink"; return true; }
            if (State.phase == "Drink") { State.phase = "Scoreboard"; return true; }
            if (State.phase != "Scoreboard") return false;
            if (tiltOnly || State.round >= RoundCount)
            { State.phase = "Finished"; State.result = "Game over! Fewest gulps wins."; }
            else BeginTurn(now);
            return true;
        }
        public bool StartTilt(double now)
        {
            if (State.phase != "Lobby" || State.players.Count < 2) return false;
            tiltOnly = true; State.phase = "Tilt"; State.round = 1; State.seconds = 63;
            State.result = ""; State.drinkId = -1; State.drinkAmount = 0;
            tiltStart = now + 3; deadline = tiltStart + 60;
            foreach (var p in State.players) { p.tiltSeconds = -1; p.gulps = 0; }
            return true;
        }
        public bool FinishTilt(int id, int round, double now)
        {
            if (State.phase != "Tilt" || round != State.round || now < tiltStart || now >= deadline) return false;
            var p = State.players.FirstOrDefault(x => x.id == id);
            if (p == null || p.tiltSeconds >= 0) return false;
            p.tiltSeconds = (float)(now - tiltStart);
            if (State.players.All(x => x.tiltSeconds >= 0)) EndTilt();
            return true;
        }
        void EndTilt()
        {
            var winner = State.players.Where(p => p.tiltSeconds >= 0).OrderBy(p => p.tiltSeconds).FirstOrDefault();
            foreach (var p in State.players) if (winner == null || p.id != winner.id) p.gulps++;
            State.result = winner == null ? "Time is up. Everyone gets 1 gulp." : winner.name + " wins Ball Tilt! Everyone else gets 1 gulp.";
            State.phase = "Scoreboard";
        }
        public bool Reset()
        {
            if (State.phase != "Finished") return false;
            State.phase = "Lobby"; State.round = 0; State.result = "";
            State.question = ""; State.answers = new string[0]; State.correctIndex = -1;
            foreach (var p in State.players) p.gulps = 0;
            return true;
        }
    }
}
