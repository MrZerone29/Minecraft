//MCCScript 1.0
//using MinecraftClient.Inventory

MCC.LoadBot(new Afk());

//MCCScript Extensions

public class Afk : ChatBot
{
    // ===== CẤU HÌNH (1 tick = 1 lần Update() của MCC ~ 100ms) =====
    private const bool SHARD = true;             // true: sau khi vao server con thi chay /warp afk | false: chi click menu roi xong
    private const string PASSWORD = "thanh2009";
    private const string MENU_TITLE = "menu";   // tiêu đề inventory cần tìm (không phân biệt hoa thường)
    private const int MENU_SLOT = 24;           // slot cần click
    private const int WAIT_AFTER_REGISTER = 40; // sau /dk
    private const int WAIT_AFTER_LOGIN = 50;    // sau /dn
    private const int MENU_TIMEOUT = 60;        // chờ menu mở sau mỗi lần /menu
    private const int WAIT_BEFORE_CLICK = 20;  // sau khi thay menu mo, cho roi moi click
    private const int MENU_MAX_TRY = 3;         // số lần thử /menu
    private const int WAIT_AFTER_SUB_JOIN = 40; // sau khi vào server con
    private const int TRANSFER_TIMEOUT = 300;   // 30s: quá hạn mà chưa thấy vào sv con thì vẫn chạy /warp afk
    // ==============================================================

    private enum Step { Idle, WaitRegister, WaitLogin, WaitMenu, WaitClick, WaitTransfer, WaitWarp }

    private Step step = Step.Idle;
    private int timer = 0;
    private int tries = 0;
    private int clickMenuId = -1;
    private bool autoEnabled = true;

    // ===== Scoreboard (SidebarReader) =====
    Dictionary<string, string> titles = new();
    Dictionary<string, Dictionary<string, int>> board = new();
    Dictionary<string, string[]> teams = new();
    Dictionary<string, string> entryTeam = new();
    string current = null;

    public override void Initialize()
    {
        RegisterChatBotCommand("scoreboard", "Show scoreboard", "scoreboard [list|all|name]", Cmd);

        if (!GetInventoryEnabled())
        {
            // Van giu scoreboard, chi tat phan tu dong login/menu/warp
            autoEnabled = false;
            LogToConsole("Can bat InventoryHandling trong MinecraftClient.ini de chay auto /menu. Chi dung scoreboard.");
            return;
        }
        LogToConsole("Afk da load, cho vao server...");
    }

    public override void AfterGameJoined()
    {
        // Reset scoreboard moi lan vao/chuyen server
        titles.Clear(); board.Clear(); teams.Clear(); entryTeam.Clear();
        current = null;

        if (!autoEnabled) return;

        if (step == Step.WaitTransfer)
        {
            // Vua chuyen sang server con
            if (SHARD)
            {
                LogToConsole("Da vao server con, cho " + WAIT_AFTER_SUB_JOIN + " tick roi /warp afk");
                step = Step.WaitWarp;
                timer = WAIT_AFTER_SUB_JOIN;
            }
            else
            {
                LogToConsole("Da vao server con. Xong (SHARD = false, khong /warp).");
                step = Step.Idle;
            }
            return;
        }

        // Vua vao server login -> chay lai tu dau
        LogToConsole("Vao server, chay /dk");
        SendText("/dk " + PASSWORD);
        step = Step.WaitRegister;
        timer = WAIT_AFTER_REGISTER;
        tries = 0;
    }

    public override void Update()
    {
        if (!autoEnabled) return;

        switch (step)
        {
            case Step.WaitRegister:
                if (--timer > 0) break;
                SendText("/dn " + PASSWORD);
                step = Step.WaitLogin;
                timer = WAIT_AFTER_LOGIN;
                break;

            case Step.WaitLogin:
                if (--timer > 0) break;
                tries = 0;
                SendMenuCommand();
                break;

            case Step.WaitMenu:
                int menuId;
                if (TryFindMenu(out menuId))
                {
                    LogToConsole("Menu mo (id " + menuId + "), cho " + WAIT_BEFORE_CLICK + " tick roi click");
                    clickMenuId = menuId;
                    step = Step.WaitClick;
                    timer = WAIT_BEFORE_CLICK;
                    break;
                }

                if (--timer > 0) break;

                if (tries < MENU_MAX_TRY)
                {
                    LogToConsole("Chua thay menu, thu lai (" + (tries + 1) + "/" + MENU_MAX_TRY + ")");
                    SendMenuCommand();
                }
                else
                {
                    LogToConsole("LOI: " + MENU_MAX_TRY + " lan /menu deu khong mo duoc. Reconnect...");
                    step = Step.Idle;
                    ReconnectToTheServer();
                }
                break;

            case Step.WaitClick:
                if (--timer > 0) break;
                LogToConsole("Click slot " + MENU_SLOT);
                WindowAction(clickMenuId, MENU_SLOT, WindowActionType.LeftClick);
                step = Step.WaitTransfer;
                timer = TRANSFER_TIMEOUT;
                break;

            case Step.WaitTransfer:
                if (--timer > 0) break;
                if (SHARD)
                {
                    LogToConsole("Qua " + (TRANSFER_TIMEOUT / 10) + "s chua thay vao server con, van chay /warp afk");
                    SendText("/warp afk");
                }
                else
                {
                    LogToConsole("Qua " + (TRANSFER_TIMEOUT / 10) + "s chua thay vao server con. Xong (SHARD = false).");
                }
                step = Step.Idle;
                break;

            case Step.WaitWarp:
                if (--timer > 0) break;
                SendText("/warp afk");
                LogToConsole("Da chay /warp afk. Xong.");
                step = Step.Idle;
                break;
        }
    }

    public override bool OnDisconnect(DisconnectReason reason, string message)
    {
        step = Step.Idle;
        return false;
    }

    private void SendMenuCommand()
    {
        tries++;
        SendText("/menu");
        step = Step.WaitMenu;
        timer = MENU_TIMEOUT;
    }

    // Tim inventory (khac inventory nguoi choi, id 0) co tieu de chua "menu"
    private bool TryFindMenu(out int id)
    {
        id = -1;
        var inventories = GetInventories();
        foreach (var pair in inventories)
        {
            if (pair.Key == 0) continue;
            string title = GetVerbatim(pair.Value.Title ?? "");
            if (title.IndexOf(MENU_TITLE, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                id = pair.Key;
                return true;
            }
        }
        return false;
    }

    // ===== Scoreboard (SidebarReader) =====
    string Cmd(string cmd, string[] args)
    {
        string arg = args == null ? "" : string.Join(" ", args).Trim();

        if (arg == "")
        {
            if (current == null || !board.ContainsKey(current)) return "Chua co scoreboard nao.";
            Print(current);
        }
        else if (arg.ToLower() == "list")
        {
            foreach (var obj in board.Keys)
                LogToConsole(obj + (obj == current ? "  (hien tai)" : "") + "  -  " + GetVerbatim(titles.ContainsKey(obj) ? titles[obj] : ""));
        }
        else if (arg.ToLower() == "all")
        {
            foreach (var obj in board.Keys.ToList()) Print(obj);
        }
        else
        {
            string found = board.Keys.FirstOrDefault(o => o.Equals(arg, StringComparison.OrdinalIgnoreCase))
                ?? board.Keys.FirstOrDefault(o => o.IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0)
                ?? board.Keys.FirstOrDefault(o => titles.ContainsKey(o) && GetVerbatim(titles[o]).IndexOf(arg, StringComparison.OrdinalIgnoreCase) >= 0);
            if (found == null) return "Khong tim thay: " + arg + " (dung /scoreboard list)";
            Print(found);
        }
        return "";
    }

    public override void OnScoreboardObjective(string objectiveName, byte mode, string objectiveValue, int type, string json, int numberFormat)
    {
        if (mode == 1)
        {
            titles.Remove(objectiveName);
            board.Remove(objectiveName);
            if (current == objectiveName) current = null;
            return;
        }
        titles[objectiveName] = objectiveValue;
        if (mode == 0)
        {
            board[objectiveName] = new();
            current = objectiveName;
        }
    }

    public override void OnUpdateScore(string entityName, int action, string objectiveName, string objectiveDisplayName, int value, int numberFormat)
    {
        if (!board.ContainsKey(objectiveName)) board[objectiveName] = new();
        if (action == 1) board[objectiveName].Remove(entityName);
        else board[objectiveName][entityName] = value;
        current = objectiveName;
    }

    public override void OnTeam(string teamName, byte method, string displayName, byte friendlyFlags,
        string nameTagVisibility, string collisionRule, int color, string prefix, string suffix, List<string> players)
    {
        if (method == 1)
        {
            teams.Remove(teamName);
            foreach (var k in entryTeam.Where(x => x.Value == teamName).Select(x => x.Key).ToList())
                entryTeam.Remove(k);
        }
        else
        {
            if (method == 0 || method == 2)
                teams[teamName] = new[] { prefix ?? "", suffix ?? "" };
            if (method == 0 || method == 3)
                foreach (var p in players) entryTeam[p] = teamName;
            if (method == 4)
                foreach (var p in players) entryTeam.Remove(p);
        }
    }

    string Line(string entry)
    {
        if (entryTeam.TryGetValue(entry, out var t) && teams.TryGetValue(t, out var ps))
            return ps[0] + entry + ps[1];
        return entry;
    }

    void Print(string obj)
    {
        titles.TryGetValue(obj, out var title);
        LogToConsole("=== " + GetVerbatim(title ?? obj) + " ===");
        foreach (var kv in board[obj].OrderByDescending(x => x.Value))
            LogToConsole(GetVerbatim(Line(kv.Key)));
    }
}