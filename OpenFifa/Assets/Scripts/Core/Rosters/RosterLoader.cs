using System;
using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Text -> <see cref="RosterLeague"/>: parse JSON, migrate to <see cref="RosterSchema.CurrentVersion"/>,
    /// validate, then build the immutable model. Never throws for bad input; problems come back in the result.
    /// </summary>
    public static class RosterLoader
    {
        public static RosterLoadResult Load(string json, string sourceName, RosterMigrator migrator = null)
        {
            JsonNode root;
            try
            {
                root = JsonParser.Parse(json);
            }
            catch (JsonParseException ex)
            {
                return RosterLoadResult.Failure(sourceName, "", ex.Message, ex.Line);
            }

            if (!root.IsObject)
                return RosterLoadResult.Failure(sourceName, "", "the file must contain a JSON object at the top level", root.Line);

            var versionNode = root.Get("schemaVersion");
            if (versionNode == null || !versionNode.IsNumber || versionNode.NumberValue != Math.Floor(versionNode.NumberValue))
                return RosterLoadResult.Failure(sourceName, "schemaVersion",
                    "required whole number is missing or invalid (current version is " + RosterSchema.CurrentVersion + ")", root.Line);
            int original = (int)versionNode.NumberValue;

            string migrationError = (migrator ?? RosterMigrator.CreateDefault()).Migrate(root, original);
            if (migrationError != null)
                return new RosterLoadResult(null, sourceName, original,
                    new List<RosterIssue> { new RosterIssue(true, "schemaVersion", migrationError, versionNode.Line) });

            var issues = RosterValidator.Validate(root);
            foreach (var i in issues)
                if (i.IsError) return new RosterLoadResult(null, sourceName, original, issues);

            return new RosterLoadResult(Build(root), sourceName, original, issues);
        }

        // Build assumes the document passed validation.
        private static RosterLeague Build(JsonNode root)
        {
            var league = root.Get("league");
            var prov = root.Get("provenance");
            var clubsNode = root.Get("clubs");
            var clubs = new RosterClub[clubsNode.Count];
            for (int c = 0; c < clubs.Length; c++) clubs[c] = BuildClub(clubsNode[c]);
            return new RosterLeague(
                root.GetInt("schemaVersion", RosterSchema.CurrentVersion),
                league.GetString("id"), league.GetString("name"), league.GetString("shortName"),
                league.GetString("country", string.Empty), league.GetString("season", string.Empty),
                prov.GetString("source"), prov.GetString("notes", string.Empty), prov.GetBool("fictional", false),
                clubs);
        }

        private static RosterClub BuildClub(JsonNode c)
        {
            var colours = c.Get("colours");
            var stadium = c.Get("stadium");
            var pn = c.Get("players");
            var players = new RosterPlayer[pn.Count];
            for (int i = 0; i < players.Length; i++) players[i] = BuildPlayer(pn[i]);
            return new RosterClub(c.GetString("id"), c.GetString("name"), c.GetString("shortName"),
                colours.GetString("primary"), colours.GetString("secondary"), colours.GetString("away"),
                stadium.GetString("name"), stadium.GetInt("capacity", 0), c.GetString("formation"),
                c.GetInt("reputation", 3), players);
        }

        private static RosterPlayer BuildPlayer(JsonNode p)
        {
            var posNode = p.Get("positions");
            var positions = new RosterPosition[posNode.Count];
            for (int i = 0; i < positions.Length; i++) RosterSchema.TryParsePosition(posNode[i].StringValue, out positions[i]);
            var attrsNode = p.Get("attributes");
            var attrs = new int[RosterSchema.AttributeCount];
            for (int i = 0; i < attrs.Length; i++) attrs[i] = attrsNode.GetInt(RosterSchema.AttributeKeys[i], RosterSchema.AttributeMin);
            RosterSchema.TryParseFoot(p.GetString("preferredFoot"), out var foot);
            return new RosterPlayer(p.GetString("id"), p.GetString("firstName"), p.GetString("lastName"),
                p.GetString("knownAs"), p.GetInt("shirtNumber", 0), p.GetString("nationality"), p.GetInt("age", 0),
                p.GetInt("heightCm", 0), foot, p.GetInt("weakFoot", 3), p.GetInt("skillMoves", 2), positions, attrs);
        }
    }
}
