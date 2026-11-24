using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace Frostline
{
    internal sealed class CardWorkbook
    {
        internal const string ResourcePath = "Config/CardWorkbook";

        internal sealed class Entry
        {
            public readonly Card Card;
            public readonly string Group, Placement;
            public readonly Row Source;

            public Entry(Card card, string group, string placement, Row source)
            {
                Card = card;
                Group = group;
                Placement = placement;
                Source = source;
            }
        }

        private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
        public IEnumerable<Entry> Additional => entries.Values.Where(entry => entry.Placement.Length > 0);

        public Entry Get(string id) => entries.TryGetValue(id, out Entry entry) ? entry :
            throw new InvalidDataException($"CardWorkbook.xlsx 缺少卡牌 ID「{id}」。");

        public static CardWorkbook Load()
        {
            var asset = Resources.Load<CardWorkbookAsset>(ResourcePath);
            if (asset == null || asset.Workbook == null || asset.Workbook.Length == 0)
                throw new InvalidDataException("未找到 Assets/Resources/Config/CardWorkbook.xlsx。");
            try
            {
                using var stream = new MemoryStream(asset.Workbook, writable: false);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                return Read(archive);
            }
            catch (Exception error) when (error is InvalidDataException or IOException or XmlException or FormatException)
            {
                throw new InvalidDataException($"CardWorkbook.xlsx 读取失败。{error.Message}", error);
            }
        }

        private static CardWorkbook Read(ZipArchive archive)
        {
            var book = new CardWorkbook();
            var reader = new Sheets(archive);
            var techniques = new Dictionary<string, (Row Source, CardTechnique Rules)>(StringComparer.Ordinal);
            foreach (Row row in reader.Rows("附加规则", "Item Rules", "ID", "目标", "次数", "横扫", "消耗", "是垃圾", "可强化手牌", "需要垃圾",
                "生成垃圾", "每回合垃圾", "用后增伤", "伤害后减计数", "随机同排", "治疗前排", "第二效果", "第二数值",
                "第三效果", "第三数值", "立即攻击", "使用次数", "敌方格挡", "连续回血", "垃圾操作"))
            {
                string id = row.Required("ID");
                var rules = new CardTechnique
                {
                    Target = row.EnumValue<CardTarget>("目标"),
                    Hits = row.Integer("次数", 1, 1),
                    Row = row.Flag("横扫"),
                    Consume = row.Flag("消耗"),
                    Junk = row.Flag("是垃圾"),
                    CanBuffHand = row.Flag("可强化手牌"),
                    JunkCost = row.Integer("需要垃圾"),
                    JunkOutput = row.Integer("生成垃圾"),
                    JunkPerTurn = row.Integer("每回合垃圾"),
                    GainAfterUse = row.Integer("用后增伤"),
                    DamageCountdown = row.Integer("伤害后减计数"),
                    RandomTarget = row.Flag("随机同排"),
                    HealFront = row.Flag("治疗前排"),
                    Secondary = row.EnumValue<EffectKind>("第二效果"),
                    SecondaryAmount = row.Integer("第二数值"),
                    ThirdEffect = row.EnumValue<EffectKind>("第三效果"),
                    ThirdAmount = row.Integer("第三数值"),
                    ImmediateHits = row.Integer("立即攻击"),
                    Uses = row.Integer("使用次数"),
                    OpponentBlock = row.Integer("敌方格挡"),
                    HealSchedule = row.IntegerList("连续回血"),
                    JunkAction = row.EnumValue<JunkOperation>("垃圾操作")
                };
                if (!techniques.TryAdd(id, (row, rules)))
                    throw row.Error($"重复的附加规则 ID「{id}」。");
            }

            foreach (Row row in reader.Rows("单位", "Units", "ID", "卡名", "分类", "生命", "攻击", "倒计时", "狂热", "攻击方式", "抗雪球",
                "能力触发", "能力效果", "能力数值", "额外数值", "插画路径", "新增投放"))
            {
                int period = row.Integer("倒计时", 3);
                EffectTrigger trigger = row.EnumValue<EffectTrigger>("能力触发");
                EffectKind effect = row.EnumValue<EffectKind>("能力效果");
                if (period == 0 && (trigger != EffectTrigger.WhenHit || effect != EffectKind.Retaliate))
                    throw row.Error("仅受击反击单位的倒计时可以为 0。");
                var ability = new UnitAbility(trigger, effect, row.Integer("能力数值"), row.Integer("额外数值"));
                var unit = new UnitType(row.Required("卡名"), row.Text("插画路径"), row.Integer("攻击"),
                    row.Integer("生命", 1, 1), period, row.EnumValue<AttackPattern>("攻击方式"),
                    row.Integer("狂热", 1, 1), ability, row.Flag("抗雪球"));
                book.Add(row, new Card(unit));
            }

            foreach (Row row in reader.Rows("道具", "Items", "ID", "卡名", "分类", "类型", "伤害", "效果", "数值", "选敌",
                "增伤条件", "增伤数值", "插画路径", "新增投放"))
            {
                string id = row.Required("ID");
                CardAction action = row.EnumValue<CardAction>("类型", required: true);
                if (action == CardAction.Deploy)
                    throw row.Error("部署类型应填在单位表。");
                techniques.TryGetValue(id, out var technique);
                if (action == CardAction.Technique && technique.Rules == null)
                    throw row.Error($"道具「{id}」需在附加规则表填写同一 ID。");
                techniques.Remove(id);
                var card = new Card(row.Required("卡名"), row.Text("插画路径"), action, row.Integer("数值"),
                    row.Integer("伤害"), row.EnumValue<EffectKind>("效果"), row.Flag("选敌"),
                    row.EnumValue<EffectKind>("增伤条件"), row.Integer("增伤数值"), technique.Rules);
                book.Add(row, card);
            }

            foreach (var orphan in techniques)
                throw orphan.Value.Source.Error($"道具表中找不到 ID「{orphan.Key}」。");
            return book;
        }

        private void Add(Row row, Card card)
        {
            string id = row.Required("ID");
            string placementText = row.Text("新增投放");
            string placement = EnglishCode(placementText) switch
            {
                "Codex" => "图鉴",
                "Battle Reward" => "战后奖励",
                _ => placementText
            };
            string groupText = row.Text("分类");
            string group = EnglishCode(groupText) switch
            {
                "Snowfolk" => "雪人",
                "Junkfolk" => "垃圾人",
                "Shared" => "通用",
                "Enemy" => "敌人",
                _ => groupText
            };
            if (placement is not ("" or "图鉴" or "战后奖励"))
                throw row.Error($"新增投放「{placement}」无效。可填图鉴或战后奖励。");
            if (placement.Length > 0 && group.Length == 0)
                throw row.Error("新增投放须填写分类。");
            if (!entries.TryAdd(id, new Entry(card, group, placement, row)))
                throw row.Error($"重复的卡牌 ID「{id}」。");
        }

        private static string EnglishCode(string text)
        {
            int separator = text.LastIndexOf(" / ", StringComparison.Ordinal);
            return separator < 0 ? text : text.Substring(separator + 3).Trim();
        }

        internal sealed class Row
        {
            private readonly string location;
            private readonly Dictionary<string, string> cells;

            public Row(string sheet, int number, Dictionary<string, string> cells)
            {
                location = $"「{sheet}」第 {number} 行";
                this.cells = cells;
            }

            public InvalidDataException Error(string message) => new($"{location}。{message}");
            public string Text(string column) => cells.TryGetValue(column, out string value) ? value : "";
            public string Required(string column) => Text(column) is { Length: > 0 } value ? value :
                throw Error($"「{column}」不能为空。");

            public int Integer(string column, int fallback = 0, int min = 0)
            {
                string text = Text(column);
                return text.Length == 0 ? fallback : ReadInteger(column, text, min);
            }

            private int ReadInteger(string column, string text, int min) =>
                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= min
                    ? value : throw Error($"「{column}」应为不小于 {min} 的整数。当前值为「{text}」。");

            public int[] IntegerList(string column) => Text(column) is { Length: > 0 } text ?
                text.Split(',', '，', '、').Select(part => ReadInteger(column, part, 1)).ToArray() : null;

            public bool Flag(string column) => EnglishCode(Text(column)) switch
            {
                "" or "0" or "否" or "No" => false,
                "1" or "是" or "Yes" => true,
                _ => throw Error($"「{column}」应填 0 或 1。当前值为「{Text(column)}」。")
            };

            public T EnumValue<T>(string column, bool required = false) where T : struct, Enum
            {
                string text = required ? Required(column) : Text(column);
                if (text.Length == 0)
                    return default;
                string code = EnglishCode(text);
                if (Enum.TryParse(code, out T value) && Enum.GetName(typeof(T), value) == code)
                    return value;
                throw Error($"「{column}」不支持「{text}」。可用值为 {string.Join("、", Enum.GetNames(typeof(T)))}。");
            }
        }

        private sealed class Sheets
        {
            private static readonly XNamespace Xml = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            private static readonly XNamespace Link = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            private readonly ZipArchive archive;
            private readonly string[] sharedStrings;
            private readonly Dictionary<string, string> sheets = new(StringComparer.Ordinal);

            public Sheets(ZipArchive archive)
            {
                this.archive = archive;
                var shared = archive.GetEntry("xl/sharedStrings.xml");
                sharedStrings = shared == null ? Array.Empty<string>() :
                    ReadXml(shared).Root.Elements(Xml + "si").Select(ReadText).ToArray();
                var relations = ReadXml(Part("xl/_rels/workbook.xml.rels")).Root.Elements().ToDictionary(
                    element => (string)element.Attribute("Id"), element => (string)element.Attribute("Target"));
                foreach (var sheet in ReadXml(Part("xl/workbook.xml")).Root.Element(Xml + "sheets").Elements(Xml + "sheet"))
                {
                    string target = relations[(string)sheet.Attribute(Link + "id")];
                    var path = new Uri(new Uri("https://workbook.invalid/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                    sheets.Add((string)sheet.Attribute("name"), Uri.UnescapeDataString(path));
                }
            }

            public IEnumerable<Row> Rows(string sheet, string englishName, params string[] columns)
            {
                string bilingualName = $"{sheet} · {englishName}";
                if (!sheets.TryGetValue(bilingualName, out string path) && !sheets.TryGetValue(sheet, out path))
                    throw new InvalidDataException($"缺少工作表「{bilingualName}」。");
                var data = ReadXml(Part(path)).Root.Element(Xml + "sheetData");
                var header = data.Elements(Xml + "row").FirstOrDefault(row => (int?)row.Attribute("r") == 1);
                if (header == null)
                    throw new InvalidDataException($"「{sheet}」第 1 行缺少列名。");

                var byColumn = new Dictionary<string, string>(StringComparer.Ordinal);
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var cell in header.Elements(Xml + "c"))
                {
                    string name = Value(cell, sheet);
                    int separator = name.IndexOf(" / ", StringComparison.Ordinal);
                    if (separator >= 0)
                        name = name.Substring(0, separator);
                    if (name.Length == 0)
                        continue;
                    if (!names.Add(name))
                        throw new InvalidDataException($"「{sheet}」列名「{name}」重复。");
                    byColumn.Add(Column(cell), name);
                }
                foreach (string column in columns)
                    if (!names.Contains(column))
                        throw new InvalidDataException($"「{sheet}」缺少列「{column}」。");
                var used = new HashSet<string>(columns, StringComparer.Ordinal);

                foreach (var line in data.Elements(Xml + "row"))
                {
                    int number = (int)line.Attribute("r");
                    if (number <= 1)
                        continue;
                    var values = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var cell in line.Elements(Xml + "c"))
                        if (byColumn.TryGetValue(Column(cell), out string name) && used.Contains(name))
                        {
                            string value = Value(cell, sheet);
                            if (value.Length > 0)
                                values.Add(name, value);
                        }
                    if (values.Count > 0)
                        yield return new Row(sheet, number, values);
                }
            }

            private static string Column(XElement cell) =>
                new(((string)cell.Attribute("r")).TakeWhile(char.IsLetter).ToArray());

            private string Value(XElement cell, string sheet)
            {
                string type = (string)cell.Attribute("t");
                if (cell.Element(Xml + "f") != null || type == "e")
                    throw new InvalidDataException($"「{sheet}」{(string)cell.Attribute("r")} 需填写普通值。");
                string value = cell.Element(Xml + "v")?.Value ?? "";
                if (type == "s")
                {
                    if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                        index >= sharedStrings.Length)
                        throw new InvalidDataException($"「{sheet}」{(string)cell.Attribute("r")} 的共享文本索引无效。");
                    value = sharedStrings[index];
                }
                if (type == "inlineStr")
                    value = ReadText(cell.Element(Xml + "is"));
                return value.Trim();
            }

            private ZipArchiveEntry Part(string path) => archive.GetEntry(path) ?? throw new InvalidDataException($"工作簿缺少 {path}。");

            private static string ReadText(XElement element) => element == null ? "" : string.Concat(
                element.Elements().Where(child => child.Name == Xml + "t" || child.Name == Xml + "r")
                    .Select(child => child.Name == Xml + "t" ? child.Value : child.Element(Xml + "t")?.Value));

            private static XDocument ReadXml(ZipArchiveEntry entry)
            {
                using var stream = entry.Open();
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    MaxCharactersInDocument = 4_000_000
                });
                return XDocument.Load(reader);
            }
        }
    }
}
