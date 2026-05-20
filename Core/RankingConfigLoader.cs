using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using BepPaths = BepInEx.Paths;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Text;
using System.Reflection;
using LiteDB;
using Application = UnityEngine.Application;

namespace Glitnir.Ranking
{
    public partial class GlitnirRankingPlugin
    {
        private void EnsureRulesFileExists()
        {
            try
            {
                if (File.Exists(_rulesFilePath))
                    return;

                File.WriteAllText(_rulesFilePath, BuildDefaultRulesFileContents(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao criar arquivo de regras: " + ex);
            }
        }

        private string BuildDefaultRulesFileContents()
        {
            StringBuilder sb = new StringBuilder(16384);

            sb.AppendLine("# =======================================================================");
            sb.AppendLine("# GLITNIR RANKING - CONFIGURACAO DE PONTUACAO");
            sb.AppendLine("# =======================================================================");
            sb.AppendLine("# Este arquivo controla as regras do ranking do servidor Glitnir.");
            sb.AppendLine("# Prefab = nome interno do item/criatura/colheita no Valheim.");
            sb.AppendLine("# Pontos = valor entregue ao jogador quando a regra for validada.");
            sb.AppendLine("#");
            sb.AppendLine("# Organizacao principal:");
            sb.AppendLine("# - KillPoints: pontos por criatura normal. Peixes NAO ficam aqui.");
            sb.AppendLine("# - FishingPoints: somente pontos por peixe realmente pescado.");
            sb.AppendLine("# - BossPoints: pontos por bosses e minibosses.");
            sb.AppendLine("# - CraftPoints.Rules: pontos por craft, em uma linha limpa separada por ponto e virgula.");
            sb.AppendLine("# - FarmJackpots.Rules: jackpots de colheita, em uma linha limpa separada por ponto e virgula.");
            sb.AppendLine("# - UniqueCraftJackpots.Rules: jackpots unicos por craft, em uma linha limpa separada por ponto e virgula.");
            sb.AppendLine("#");
            sb.AppendLine("# Nao use virgulas nas listas de producao/jackpot. Use ;");
            sb.AppendLine("# Exemplos:");
            sb.AppendLine("#   CraftPoints.Rules=MeadHealthMinor:20;ArrowNeedle:2");
            sb.AppendLine("#   FarmJackpots.Rules=Carrot:200:1000");
            sb.AppendLine("#   UniqueCraftJackpots.Rules=IronSword:1:800");
            sb.AppendLine("# =======================================================================");
            sb.AppendLine();
            sb.AppendLine("RankingEnabled=true");
            sb.AppendLine("EnableKillPoints=true");
            sb.AppendLine("EnableBossPoints=true");
            sb.AppendLine("DefaultKillPoints=1");
            sb.AppendLine("TopCount=10");
            sb.AppendLine("AllowRepeatedBossPoints=false");
            sb.AppendLine();
            sb.AppendLine("# Logging");
            sb.AppendLine("DebugLogging=false");
            sb.AppendLine("LogHitReports=false");
            sb.AppendLine("LogKillReports=false");
            sb.AppendLine("LogSkillReports=false");
            sb.AppendLine("LogPendingKillReports=false");
            sb.AppendLine("LogSnapshotRequests=false");
            sb.AppendLine("LogPointsChanges=false");
            sb.AppendLine();
            sb.AppendLine("# Skill milestone jackpot points");
            sb.AppendLine("EnableSkillPoints=true");
            sb.AppendLine("IgnoreTamedKills=true");
            sb.AppendLine("EnableMarketplaceQuestPoints=true");
            sb.AppendLine("EnableFishingPoints=true");
            sb.AppendLine("EnableDeathPenalty=true");
            sb.AppendLine();

            sb.AppendLine("# Marketplace quest points");
            sb.AppendLine("[MarketplaceQuestPoints]");
            sb.AppendLine("QuestPointMap=ccq_b21:450,ccq_b22:150");
            sb.AppendLine();

            sb.AppendLine("# Penalidade por morte");
            sb.AppendLine("[DeathPenalty]");
            sb.AppendLine("# UseMultiplierMode=false usa a tabela Rules abaixo.");
            sb.AppendLine("# UseMultiplierMode=true ignora Rules e usa PenaltyPerDeath por morte.");
            sb.AppendLine("# Exemplo multiplicador: PenaltyPerDeath=100 e 15 mortes = -1500.");
            sb.AppendLine("UseMultiplierMode=false");
            sb.AppendLine("PenaltyPerDeath=100");
            sb.AppendLine("# Formato do modo tabela: quantidadeDeMortes:pontosPerdidos,quantidadeDeMortes:pontosPerdidos");
            sb.AppendLine("# Exemplo: 1:0,2:100,5:300,10:800");
            sb.AppendLine("Rules=1:0,2:100,5:300,10:800");
            sb.AppendLine();

            sb.AppendLine("# Reward claims");
            sb.AppendLine("RewardClaimsEnabled=true");
            sb.AppendLine("RewardClaimCycleId=temporada_001");
            sb.AppendLine("RewardTop1MinPoints=300");
            sb.AppendLine("RewardTop1Label=Recompensa do 1 lugar");
            sb.AppendLine("RewardTop1Prefab=Coins");
            sb.AppendLine("RewardTop1Amount=500");
            sb.AppendLine("RewardTop2MinPoints=200");
            sb.AppendLine("RewardTop2Label=Recompensa do 2 lugar");
            sb.AppendLine("RewardTop2Prefab=AmberPearl");
            sb.AppendLine("RewardTop2Amount=10");
            sb.AppendLine("RewardTop3MinPoints=100");
            sb.AppendLine("RewardTop3Label=Recompensa do 3 lugar");
            sb.AppendLine("RewardTop3Prefab=Ruby");
            sb.AppendLine("RewardTop3Amount=5");
            sb.AppendLine();

            AppendBossPoints(sb);
            AppendKillPoints(sb);
            AppendFishingPoints(sb);
            AppendSkillMilestoneJackpotPoints(sb);
            AppendProductionRankingRules(sb);

            return sb.ToString();
        }

        private void AppendBossPoints(StringBuilder sb)
        {
            sb.AppendLine("# =========================");
            sb.AppendLine("# BOSS POINTS");
            sb.AppendLine("# =========================");
            sb.AppendLine("# Cada boss/miniboss fica em uma chave própria.");
            sb.AppendLine("BossPoints.Bonemass=2200");
            sb.AppendLine("BossPoints.Charred_Melee_Dyrnwyn=40");
            sb.AppendLine("BossPoints.Dragon=3000");
            sb.AppendLine("BossPoints.Eikthyr=1200");
            sb.AppendLine("BossPoints.Fader=10000");
            sb.AppendLine("BossPoints.Fenring_Cultist_Hildir=30");
            sb.AppendLine("BossPoints.gd_king=4200");
            sb.AppendLine("BossPoints.GoblinBruteBros=35");
            sb.AppendLine("BossPoints.GoblinBrute_Hildir=18");
            sb.AppendLine("BossPoints.GoblinKing=6000");
            sb.AppendLine("BossPoints.GoblinShaman_Hildir=18");
            sb.AppendLine("BossPoints.SeekerQueen=8600");
            sb.AppendLine("BossPoints.Skeleton_Hildir=25");
            sb.AppendLine();
        }

        private void AppendPoint(StringBuilder sb, string section, string key, int points)
        {
            sb.AppendLine(section + "." + key + "=" + points);
        }

        private void AppendKillPoint(StringBuilder sb, string prefabName, int points, string comment = null)
        {

        }

        private void AppendKillPoints(StringBuilder sb)
        {
            sb.AppendLine("# =========================");
            sb.AppendLine("# KILL POINTS / COMBATE");
            sb.AppendLine("# =========================");
            sb.AppendLine("# Pontos por criaturas normais. Peixes nao devem ficar aqui; use [FishingPoints].");
            sb.AppendLine("# Formato gerado pelo BepInEx: [KillPoints] Prefab = pontos.");
            sb.AppendLine("# Mantido no modo performance: o mod NAO envia RPC por hit; pontua apenas kill confirmada.");
            sb.AppendLine("KillPoints.Boar=1");
            sb.AppendLine("KillPoints.Neck=1");
            sb.AppendLine("KillPoints.Greyling=1");
            sb.AppendLine("KillPoints.Greydwarf=1");
            sb.AppendLine("KillPoints.Greydwarf_Elite=2");
            sb.AppendLine("KillPoints.Greydwarf_Shaman=2");
            sb.AppendLine("KillPoints.Skeleton=1");
            sb.AppendLine("KillPoints.Troll=5");
            sb.AppendLine("KillPoints.Draugr=2");
            sb.AppendLine("KillPoints.Draugr_Elite=3");
            sb.AppendLine("KillPoints.Blob=2");
            sb.AppendLine("KillPoints.Wolf=2");
            sb.AppendLine("KillPoints.Hatchling=2");
            sb.AppendLine("KillPoints.StoneGolem=8");
            sb.AppendLine("KillPoints.Deathsquito=3");
            sb.AppendLine("KillPoints.Goblin=3");
            sb.AppendLine("KillPoints.GoblinBrute=5");
            sb.AppendLine("KillPoints.GoblinShaman=4");
            sb.AppendLine("KillPoints.Lox=6");
            sb.AppendLine("KillPoints.Serpent=6");
            sb.AppendLine("KillPoints.Seeker=4");
            sb.AppendLine("KillPoints.SeekerBrute=8");
            sb.AppendLine("KillPoints.Gjall=10");
            sb.AppendLine("KillPoints.Charred_Melee=4");
            sb.AppendLine("KillPoints.Charred_Archer=4");
            sb.AppendLine("KillPoints.Morgen=12");
            sb.AppendLine();
        }

        private void AppendFishingPoints(StringBuilder sb)
        {
            sb.AppendLine("# =========================");
            sb.AppendLine("# FISHING POINTS");
            sb.AppendLine("# =========================");
            sb.AppendLine("# As chaves podem ficar em português; o código converte para o prefab real internamente.");
            sb.AppendLine("# Mapeamento: Perca=Fish1, Lúcio=Fish2, Fish3=Fish3, Tetra=Fish4_cave, Peixe Troll=Fish5, Arenque gigante=Fish6, Garoupa=Fish7, Garoupa estrelada=Fish8, Peixe-pescador=Fish9, Salmão do norte=Fish10, Peixe magma=Fish11, Baiacu=Fish12");
            AppendFishingPoint(sb, "Perca", 1);
            AppendFishingPoint(sb, "Lúcio", 1);
            AppendFishingPoint(sb, "Fish3", 2);
            AppendFishingPoint(sb, "Tetra", 3);
            AppendFishingPoint(sb, "Peixe Troll", 2);
            AppendFishingPoint(sb, "Arenque gigante", 3);
            AppendFishingPoint(sb, "Garoupa", 3000);
            AppendFishingPoint(sb, "Garoupa estrelada", 4);
            AppendFishingPoint(sb, "Peixe-pescador", 4);
            AppendFishingPoint(sb, "Salmão do norte", 5);
            AppendFishingPoint(sb, "Peixe magma", 5);
            AppendFishingPoint(sb, "Baiacu", 6);
            sb.AppendLine();
        }

        private void AppendFishingPoint(StringBuilder sb, string prefabName, int points)
        {
            sb.AppendLine("FishingPoints." + prefabName + "=" + points);
        }

        private void AppendSkillPoint(StringBuilder sb, string skillKey, int points, string comment = null)
        {
            if (!string.IsNullOrWhiteSpace(comment))
                sb.AppendLine(comment);

            sb.AppendLine("SkillPoints." + skillKey + "=" + points);
        }

        private void AppendSkillMilestoneJackpotPoints(StringBuilder sb)
        {
            sb.AppendLine("# =========================");
            sb.AppendLine("# SKILL MILESTONE JACKPOT POINTS");
            sb.AppendLine("# =========================");
            sb.AppendLine("# Pontos únicos por marco de skill. Formato: SkillMilestoneJackpotPoints.Skill=20:pontos,40:pontos,60:pontos,80:pontos,100:pontos");
            sb.AppendLine("# Exemplo: Bows 20/40/60/80/100. Cada marco pontua uma única vez por jogador/ciclo.");
            AppendSkillMilestoneJackpotPoint(sb, "BloodMagic", "20:1500,40:3000,60:5000,80:7500,100:12000");
            AppendSkillMilestoneJackpotPoint(sb, "ElementalMagic", "20:1000,40:2000,60:3500,80:5500,100:8000");
            AppendSkillMilestoneJackpotPoint(sb, "Bows", "20:500,40:1000,60:2000,80:3000,100:4000");
            AppendSkillMilestoneJackpotPoint(sb, "Crossbows", "20:500,40:1000,60:2000,80:3000,100:4000");
            AppendSkillMilestoneJackpotPoint(sb, "Swords", "20:500,40:1000,60:2000,80:3000,100:4000");
            AppendSkillMilestoneJackpotPoint(sb, "Axes", "20:500,40:1000,60:2000,80:3000,100:4000");
            AppendSkillMilestoneJackpotPoint(sb, "Clubs", "20:500,40:1000,60:2000,80:3000,100:4000");
            AppendSkillMilestoneJackpotPoint(sb, "Spears", "20:500,40:1000,60:2000,80:3000,100:4000");
            AppendSkillMilestoneJackpotPoint(sb, "Polearms", "20:500,40:1000,60:2000,80:3000,100:4000");
            AppendSkillMilestoneJackpotPoint(sb, "Knives", "20:500,40:1000,60:2000,80:3000,100:4000");
            AppendSkillMilestoneJackpotPoint(sb, "Blocking", "20:400,40:800,60:1500,80:2500,100:3500");
            AppendSkillMilestoneJackpotPoint(sb, "Run", "20:300,40:700,60:1200,80:2000,100:3000");
            AppendSkillMilestoneJackpotPoint(sb, "Jump", "20:300,40:700,60:1200,80:2000,100:3000");
            AppendSkillMilestoneJackpotPoint(sb, "WoodCutting", "20:300,40:700,60:1200,80:1800,100:2500");
            AppendSkillMilestoneJackpotPoint(sb, "Pickaxes", "20:300,40:700,60:1200,80:1800,100:2500");
            AppendSkillMilestoneJackpotPoint(sb, "Farming", "20:0,40:0,60:0,80:0,100:0");
            sb.AppendLine();
        }

        private void AppendSkillMilestoneJackpotPoint(StringBuilder sb, string skillKey, string milestones)
        {
            sb.AppendLine("SkillMilestoneJackpotPoints." + skillKey + "=" + milestones);
        }

        private void AppendProductionRankingRules(StringBuilder sb)
        {
            sb.AppendLine("# =======================================================================");
            sb.AppendLine("# PRODUCAO E JACKPOTS - GLITNIR RANKING");
            sb.AppendLine("# =======================================================================");
            sb.AppendLine("# Estes blocos usam UMA entrada por sistema para manter o arquivo limpo.");
            sb.AppendLine("# Nao use virgulas. Separe cada regra com ponto e virgula (;).");
            sb.AppendLine("# Formatos:");
            sb.AppendLine("#   CraftPoints.Rules = Prefab:pontos;OutroPrefab:pontos");
            sb.AppendLine("#   FarmJackpots.Rules = Prefab:quantidade:pontos;OutroPrefab:quantidade:pontos");
            sb.AppendLine("#   UniqueCraftJackpots.Rules = Prefab:quantidade:pontos;OutroPrefab:quantidade:pontos");
            sb.AppendLine("# Exemplos reais:");
            sb.AppendLine("#   MeadHealthMinor:20");
            sb.AppendLine("#   Carrot:200:1000");
            sb.AppendLine("#   IronSword:1:800");
            sb.AppendLine("# =======================================================================");
            sb.AppendLine();

            sb.AppendLine("# =========================");
            sb.AppendLine("# CRAFT POINTS");
            sb.AppendLine("# =========================");
            sb.AppendLine("# Pontos por craft de qualquer prefab configurado.");
            sb.AppendLine("# Exemplo de item: MeadHealthMinor:20");
            sb.AppendLine("CraftPoints.Rules=MeadHealthMinor:20");
            sb.AppendLine();

            sb.AppendLine("# =========================");
            sb.AppendLine("# FARM JACKPOTS");
            sb.AppendLine("# =========================");
            sb.AppendLine("# Jackpot por colheita. Premia uma vez por jogador/ciclo quando bater a quantidade.");
            sb.AppendLine("# Exemplo de item: Carrot:200:1000");
            sb.AppendLine("FarmJackpots.Rules=Carrot:200:1000");
            sb.AppendLine();

            sb.AppendLine("# =========================");
            sb.AppendLine("# UNIQUE CRAFT JACKPOTS");
            sb.AppendLine("# =========================");
            sb.AppendLine("# Jackpot unico por craft. Ideal para armas, armaduras e marcos especiais.");
            sb.AppendLine("# Exemplo de item: IronSword:1:800");
            sb.AppendLine("UniqueCraftJackpots.Rules=IronSword:1:800");
            sb.AppendLine();
        }

        private void AppendStringPoint(StringBuilder sb, string section, string key, string value)
        {
            sb.AppendLine(section + "." + key + "=" + value);
        }

        private void LoadRules()
        {
            _rules = new RankingRules();
            try
            {
                if (!File.Exists(_rulesFilePath))
                    return;

                foreach (string rawLine in File.ReadAllLines(_rulesFilePath, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//"))
                        continue;

                    int eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0)
                        continue;

                    string key = line.Substring(0, eqIndex).Trim();
                    string value = line.Substring(eqIndex + 1).Trim();

                    if (key.Equals("RankingEnabled", StringComparison.OrdinalIgnoreCase))
                        _rules.RankingEnabled = ParseBool(value, _rules.RankingEnabled);
                    else if (key.Equals("EnableKillPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.EnableKillPoints = ParseBool(value, _rules.EnableKillPoints);
                    else if (key.Equals("EnableBossPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.EnableBossPoints = ParseBool(value, _rules.EnableBossPoints);
                    else if (key.Equals("DefaultKillPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.DefaultKillPoints = ParseInt(value, _rules.DefaultKillPoints);
                    else if (key.Equals("TopCount", StringComparison.OrdinalIgnoreCase))
                        _rules.TopCount = Mathf.Clamp(ParseInt(value, _rules.TopCount), 1, 50);
                    else if (key.Equals("AllowRepeatedBossPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.AllowRepeatedBossPoints = ParseBool(value, _rules.AllowRepeatedBossPoints);
                    else if (key.Equals("DebugLogging", StringComparison.OrdinalIgnoreCase))
                        _rules.DebugLogging = ParseBool(value, _rules.DebugLogging);
                    else if (key.Equals("LogHitReports", StringComparison.OrdinalIgnoreCase))
                        _rules.LogHitReports = ParseBool(value, _rules.LogHitReports);
                    else if (key.Equals("LogKillReports", StringComparison.OrdinalIgnoreCase))
                        _rules.LogKillReports = ParseBool(value, _rules.LogKillReports);
                    else if (key.Equals("LogPendingKillReports", StringComparison.OrdinalIgnoreCase))
                        _rules.LogPendingKillReports = ParseBool(value, _rules.LogPendingKillReports);
                    else if (key.Equals("LogSnapshotRequests", StringComparison.OrdinalIgnoreCase))
                        _rules.LogSnapshotRequests = ParseBool(value, _rules.LogSnapshotRequests);
                    else if (key.Equals("LogPointsChanges", StringComparison.OrdinalIgnoreCase))
                        _rules.LogPointsChanges = ParseBool(value, _rules.LogPointsChanges);
                    else if (key.Equals("EnableSkillPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.EnableSkillPoints = ParseBool(value, _rules.EnableSkillPoints);
                    else if (key.Equals("LogSkillReports", StringComparison.OrdinalIgnoreCase))
                        _rules.LogSkillReports = ParseBool(value, _rules.LogSkillReports);
                    else if (key.Equals("EnableFishingPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.EnableFishingPoints = ParseBool(value, _rules.EnableFishingPoints);
                    else if (key.Equals("EnableDeathPenalty", StringComparison.OrdinalIgnoreCase))
                        _rules.EnableDeathPenalty = ParseBool(value, _rules.EnableDeathPenalty);
                    else if (key.Equals("DeathPenalty.UseMultiplierMode", StringComparison.OrdinalIgnoreCase) || key.Equals("DeathPenaltyUseMultiplier", StringComparison.OrdinalIgnoreCase) || key.Equals("UseMultiplierMode", StringComparison.OrdinalIgnoreCase))
                        _rules.DeathPenaltyUseMultiplier = ParseBool(value, _rules.DeathPenaltyUseMultiplier);
                    else if (key.Equals("DeathPenalty.PenaltyPerDeath", StringComparison.OrdinalIgnoreCase) || key.Equals("DeathPenaltyPerDeath", StringComparison.OrdinalIgnoreCase) || key.Equals("PenaltyPerDeath", StringComparison.OrdinalIgnoreCase))
                        _rules.DeathPenaltyPerDeath = Mathf.Max(0, ParseInt(value, _rules.DeathPenaltyPerDeath));
                    else if (key.Equals("DeathPenalty.Rules", StringComparison.OrdinalIgnoreCase) || key.Equals("DeathPenaltyRules", StringComparison.OrdinalIgnoreCase) || key.Equals("Rules", StringComparison.OrdinalIgnoreCase))
                        _rules.DeathPenaltyRules = ParseDeathPenaltyRules(value);
                    else if (key.Equals("RewardClaimsEnabled", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardClaimsEnabled = ParseBool(value, _rules.RewardClaimsEnabled);
                    else if (key.Equals("RewardClaimCycleId", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardClaimCycleId = SafeLimit(value, 64);
                    else if (key.Equals("RewardTop1MinPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop1MinPoints = Mathf.Max(0, ParseInt(value, _rules.RewardTop1MinPoints));
                    else if (key.Equals("RewardTop1Label", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop1Label = SafeLimit(value, 96);
                    else if (key.Equals("RewardTop1Prefab", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop1Prefab = SafeLimit(value, 96);
                    else if (key.Equals("RewardTop1Amount", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop1Amount = Mathf.Max(0, ParseInt(value, _rules.RewardTop1Amount));
                    else if (key.Equals("RewardTop2MinPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop2MinPoints = Mathf.Max(0, ParseInt(value, _rules.RewardTop2MinPoints));
                    else if (key.Equals("RewardTop2Label", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop2Label = SafeLimit(value, 96);
                    else if (key.Equals("RewardTop2Prefab", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop2Prefab = SafeLimit(value, 96);
                    else if (key.Equals("RewardTop2Amount", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop2Amount = Mathf.Max(0, ParseInt(value, _rules.RewardTop2Amount));
                    else if (key.Equals("RewardTop3MinPoints", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop3MinPoints = Mathf.Max(0, ParseInt(value, _rules.RewardTop3MinPoints));
                    else if (key.Equals("RewardTop3Label", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop3Label = SafeLimit(value, 96);
                    else if (key.Equals("RewardTop3Prefab", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop3Prefab = SafeLimit(value, 96);
                    else if (key.Equals("RewardTop3Amount", StringComparison.OrdinalIgnoreCase))
                        _rules.RewardTop3Amount = Mathf.Max(0, ParseInt(value, _rules.RewardTop3Amount));
                    else if (key.StartsWith("KillPoints.", StringComparison.OrdinalIgnoreCase))
                    {
                        string killKey = SafeKey(key.Substring("KillPoints.".Length));
                        if (!IsFishPrefab(killKey))
                            _rules.KillPoints[killKey] = ParseInt(value, 0);
                    }
                    else if (key.StartsWith("BossPoints.", StringComparison.OrdinalIgnoreCase))
                        _rules.BossPoints[NormalizeBossPrefabName(key.Substring("BossPoints.".Length))] = ParseInt(value, 0);
                    else if (key.StartsWith("FishingPoints.", StringComparison.OrdinalIgnoreCase))
                    {
                        string fishKey = NormalizeFishPrefabName(key.Substring("FishingPoints.".Length));
                        if (IsFishPrefab(fishKey))
                            _rules.FishingPoints[fishKey] = ParseInt(value, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao carregar regras do ranking: " + ex);
            }

        }

        private bool ParseBool(string raw, bool fallback)
        {
            bool value;
            return bool.TryParse(raw, out value) ? value : fallback;
        }

        private int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse(raw, out value) ? value : fallback;
        }

        private void InitializeSyncedRulesConfig()
        {
            try
            {


                string legacyRulesFilePath = Path.Combine(BepPaths.ConfigPath, "glitnir.ranking.rules.cfg");
                Dictionary<string, string> legacyOverrides = LoadLegacyRulesOverrides(legacyRulesFilePath);
                MergeSectionOverrides(legacyOverrides, LoadSectionRulesOverrides(legacyRulesFilePath));

                _rulesConfig = Config;
                // ServerSync registra cada entrada sincronizada via BindConfig(..., synced: true).

                BindSyncedRulesConfigEntries(legacyOverrides);
                ApplyRulesFromSyncedConfig();
                _rulesConfig.Save();

                CleanupFishKillPointConfigEntries();
                RewriteRulesConfigHeaderAndGroupedSections();
            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao inicializar config sincronizada do ranking: " + ex);
                EnsureRulesFileExists();
                LoadRules();
            }
        }

        private void SetupRulesConfigWatcher()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_rulesFilePath))
                    return;

                string directory = Path.GetDirectoryName(_rulesFilePath);
                string fileName = Path.GetFileName(_rulesFilePath);

                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                    return;

                _rulesConfigWatcher = new FileSystemWatcher(directory, fileName);
                _rulesConfigWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName;
                _rulesConfigWatcher.Changed += OnRulesConfigFileChanged;
                _rulesConfigWatcher.Created += OnRulesConfigFileChanged;
                _rulesConfigWatcher.Renamed += OnRulesConfigFileChanged;
                _rulesConfigWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Não foi possível iniciar watcher da config do ranking: " + ex.Message);
            }
        }

        private void OnRulesConfigFileChanged(object sender, FileSystemEventArgs args)
        {
            _rulesConfigReloadQueued = true;
            _rulesConfigReloadAt = Time.realtimeSinceStartup + 0.5f;
        }

        private void ProcessRulesConfigReloadIfNeeded()
        {
            if (!_rulesConfigReloadQueued)
                return;

            if (Time.realtimeSinceStartup < _rulesConfigReloadAt)
                return;

            _rulesConfigReloadQueued = false;

            try
            {
                _rulesConfig?.Reload();
                BindSyncedRulesConfigEntries(null);
                ApplyRulesFromSyncedConfig();

            }
            catch (Exception ex)
            {
                Logger.LogError("Erro ao recarregar config do ranking: " + ex);
            }
        }

        private void RefreshRulesFromSyncedConfigIfNeeded()
        {
            if (Time.realtimeSinceStartup < _rulesConfigApplyAt)
                return;

            _rulesConfigApplyAt = Time.realtimeSinceStartup + RulesConfigApplyInterval;
            ApplyRulesFromSyncedConfig();
        }

        private Dictionary<string, string> LoadLegacyRulesOverrides(string filePath)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return result;

                bool hasSectionHeader = false;
                foreach (string rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        hasSectionHeader = true;
                        break;
                    }
                }

                if (hasSectionHeader)
                    return result;

                foreach (string rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//"))
                        continue;

                    int eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0)
                        continue;

                    string key = line.Substring(0, eqIndex).Trim();
                    string value = line.Substring(eqIndex + 1).Trim();

                    if (!string.IsNullOrWhiteSpace(key))
                        result[key] = value;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Falha ao ler overrides legados da config do ranking: " + ex.Message);
            }

            return result;
        }

        private void MergeSectionOverrides(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            if (target == null || source == null)
                return;

            foreach (KeyValuePair<string, string> pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                    target[pair.Key] = pair.Value;
            }
        }

        private Dictionary<string, string> LoadSectionRulesOverrides(string filePath)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return result;

                string currentSection = string.Empty;
                HashSet<string> supportedDynamicSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "BossPoints",
                    "FishingPoints",
                    "KillPoints",
                    "CraftPoints",
                    "FarmJackpots",
                    "UniqueCraftJackpots",
                    "SkillMilestoneJackpotPoints",
                    "DeathPenalty",
                    "ExplorationMapJackpots"
                };

                foreach (string rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string line = rawLine != null ? rawLine.Trim() : string.Empty;
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//"))
                        continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = SafeKey(line.Substring(1, line.Length - 2));
                        continue;
                    }

                    int eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0)
                        continue;

                    string key = line.Substring(0, eqIndex).Trim();
                    string value = line.Substring(eqIndex + 1).Trim();

                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    string fullKey = key;
                    if (!key.Contains(".") && !string.IsNullOrWhiteSpace(currentSection))
                        fullKey = currentSection + "." + key;

                    int dotIndex = fullKey.IndexOf('.');
                    if (dotIndex <= 0)
                        continue;

                    string section = fullKey.Substring(0, dotIndex).Trim();
                    string entryKey = SafeKey(fullKey.Substring(dotIndex + 1));
                    if (!supportedDynamicSections.Contains(section) || string.IsNullOrWhiteSpace(entryKey))
                        continue;

                    if (section.Equals("KillPoints", StringComparison.OrdinalIgnoreCase) && IsFishPrefab(entryKey))
                        continue;

                    result[section + "." + entryKey] = value;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Falha ao ler entradas dinâmicas da config do ranking: " + ex.Message);
            }

            return result;
        }

        private bool IsLegacyRulesFileFormat(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return false;

                foreach (string rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//"))
                        continue;

                    return !(line.StartsWith("[") && line.EndsWith("]"));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Falha ao detectar formato legado da config do ranking: " + ex.Message);
            }

            return false;
        }

        private void BindSyncedRulesConfigEntries(Dictionary<string, string> legacyOverrides)
        {
            if (_rulesConfig == null)
                return;

            legacyOverrides = legacyOverrides ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            RankingRules defaults = new RankingRules();

            _cfgRankingEnabled = _rulesConfig.BindConfig("General", "RankingEnabled",
                ReadLegacyBool(legacyOverrides, "RankingEnabled", defaults.RankingEnabled),
                "Ativa o sistema de ranking.", synced: true);

            _cfgEnableKillPoints = _rulesConfig.BindConfig("General", "EnableKillPoints",
                ReadLegacyBool(legacyOverrides, "EnableKillPoints", defaults.EnableKillPoints),
                "Ativa a pontuação por criaturas normais.", synced: true);

            _cfgEnableBossPoints = _rulesConfig.BindConfig("General", "EnableBossPoints",
                ReadLegacyBool(legacyOverrides, "EnableBossPoints", defaults.EnableBossPoints),
                "Ativa a pontuação por bosses e minibosses.", synced: true);

            _cfgDefaultKillPoints = _rulesConfig.BindConfig("General", "DefaultKillPoints",
                ReadLegacyInt(legacyOverrides, "DefaultKillPoints", defaults.DefaultKillPoints),
                "Pontos padrão para kills sem regra específica.", synced: true);

            _cfgTopCount = _rulesConfig.BindConfig("General", "TopCount",
                ReadLegacyInt(legacyOverrides, "TopCount", defaults.TopCount),
                "Quantidade de jogadores exibidos no topo do ranking.", synced: true);

            _cfgAllowRepeatedBossPoints = _rulesConfig.BindConfig("General", "AllowRepeatedBossPoints",
                ReadLegacyBool(legacyOverrides, "AllowRepeatedBossPoints", defaults.AllowRepeatedBossPoints),
                "Permite pontuar boss repetido.", synced: true);

            _cfgEnableSkillPoints = _rulesConfig.BindConfig("General", "EnableSkillPoints",
                ReadLegacyBool(legacyOverrides, "EnableSkillPoints", defaults.EnableSkillPoints),
                "Ativa jackpots por marcos de skill.", synced: true);


            _cfgIgnoreTamedKills = _rulesConfig.BindConfig("General", "IgnoreTamedKills",
                ReadLegacyBool(legacyOverrides, "IgnoreTamedKills", defaults.IgnoreTamedKills),
                "Ignora pontos por matar criaturas domadas.", synced: true);

            _cfgEnableMarketplaceQuestPoints = _rulesConfig.BindConfig("General", "EnableMarketplaceQuestPoints",
                ReadLegacyBool(legacyOverrides, "EnableMarketplaceQuestPoints", defaults.EnableMarketplaceQuestPoints),
                "Ativa pontuação por conclusão de quests do Marketplace.", synced: true);

            _cfgEnableFishingPoints = _rulesConfig.BindConfig("General", "EnableFishingPoints",
                ReadLegacyBool(legacyOverrides, "EnableFishingPoints", defaults.EnableFishingPoints),
                "Ativa pontuação própria por pesca.", synced: true);

            _cfgEnableDeathPenalty = _rulesConfig.BindConfig("General", "EnableDeathPenalty",
                ReadLegacyBool(legacyOverrides, "EnableDeathPenalty", defaults.EnableDeathPenalty),
                "Ativa penalidade por morte no ranking.", synced: true);

            _cfgDebugLogging = _rulesConfig.BindConfig("Logging", "DebugLogging",
                ReadLegacyBool(legacyOverrides, "DebugLogging", defaults.DebugLogging),
                "Ativa logs de depuração.", synced: true);

            _cfgLogHitReports = _rulesConfig.BindConfig("Logging", "LogHitReports",
                ReadLegacyBool(legacyOverrides, "LogHitReports", defaults.LogHitReports),
                "Ativa logs de hit reports.", synced: true);

            _cfgLogKillReports = _rulesConfig.BindConfig("Logging", "LogKillReports",
                ReadLegacyBool(legacyOverrides, "LogKillReports", defaults.LogKillReports),
                "Ativa logs de kill reports.", synced: true);

            _cfgLogSkillReports = _rulesConfig.BindConfig("Logging", "LogSkillReports",
                ReadLegacyBool(legacyOverrides, "LogSkillReports", defaults.LogSkillReports),
                "Ativa logs de skill reports.", synced: true);

            _cfgLogPendingKillReports = _rulesConfig.BindConfig("Logging", "LogPendingKillReports",
                ReadLegacyBool(legacyOverrides, "LogPendingKillReports", defaults.LogPendingKillReports),
                "Ativa logs das checagens pendentes de kill.", synced: true);

            _cfgLogSnapshotRequests = _rulesConfig.BindConfig("Logging", "LogSnapshotRequests",
                ReadLegacyBool(legacyOverrides, "LogSnapshotRequests", defaults.LogSnapshotRequests),
                "Ativa logs de snapshots do ranking.", synced: true);

            _cfgLogPointsChanges = _rulesConfig.BindConfig("Logging", "LogPointsChanges",
                ReadLegacyBool(legacyOverrides, "LogPointsChanges", defaults.LogPointsChanges),
                "Ativa logs de alterações de pontos.", synced: true);

            _cfgRewardClaimsEnabled = _rulesConfig.BindConfig("Rewards", "RewardClaimsEnabled",
                ReadLegacyBool(legacyOverrides, "RewardClaimsEnabled", defaults.RewardClaimsEnabled),
                "Ativa o resgate de recompensas no HUD.", synced: true);

            _cfgRewardClaimCycleId = _rulesConfig.BindConfig("Rewards", "RewardClaimCycleId",
                ReadLegacyString(legacyOverrides, "RewardClaimCycleId", defaults.RewardClaimCycleId),
                "Identificador do ciclo atual de recompensas.", synced: true);

            _cfgRewardTop1MinPoints = _rulesConfig.BindConfig("Rewards", "RewardTop1MinPoints",
                ReadLegacyInt(legacyOverrides, "RewardTop1MinPoints", defaults.RewardTop1MinPoints),
                "Pontos mínimos para o Top 1 resgatar.", synced: true);

            _cfgRewardTop1Label = _rulesConfig.BindConfig("Rewards", "RewardTop1Label",
                ReadLegacyString(legacyOverrides, "RewardTop1Label", defaults.RewardTop1Label),
                "Texto exibido para a recompensa do Top 1.", synced: true);

            _cfgRewardTop1Prefab = _rulesConfig.BindConfig("Rewards", "RewardTop1Prefab",
                ReadLegacyString(legacyOverrides, "RewardTop1Prefab", defaults.RewardTop1Prefab),
                "Prefab do item entregue ao Top 1.", synced: true);

            _cfgRewardTop1Amount = _rulesConfig.BindConfig("Rewards", "RewardTop1Amount",
                ReadLegacyInt(legacyOverrides, "RewardTop1Amount", defaults.RewardTop1Amount),
                "Quantidade do item entregue ao Top 1.", synced: true);

            _cfgRewardTop2MinPoints = _rulesConfig.BindConfig("Rewards", "RewardTop2MinPoints",
                ReadLegacyInt(legacyOverrides, "RewardTop2MinPoints", defaults.RewardTop2MinPoints),
                "Pontos mínimos para o Top 2 resgatar.", synced: true);

            _cfgRewardTop2Label = _rulesConfig.BindConfig("Rewards", "RewardTop2Label",
                ReadLegacyString(legacyOverrides, "RewardTop2Label", defaults.RewardTop2Label),
                "Texto exibido para a recompensa do Top 2.", synced: true);

            _cfgRewardTop2Prefab = _rulesConfig.BindConfig("Rewards", "RewardTop2Prefab",
                ReadLegacyString(legacyOverrides, "RewardTop2Prefab", defaults.RewardTop2Prefab),
                "Prefab do item entregue ao Top 2.", synced: true);

            _cfgRewardTop2Amount = _rulesConfig.BindConfig("Rewards", "RewardTop2Amount",
                ReadLegacyInt(legacyOverrides, "RewardTop2Amount", defaults.RewardTop2Amount),
                "Quantidade do item entregue ao Top 2.", synced: true);

            _cfgRewardTop3MinPoints = _rulesConfig.BindConfig("Rewards", "RewardTop3MinPoints",
                ReadLegacyInt(legacyOverrides, "RewardTop3MinPoints", defaults.RewardTop3MinPoints),
                "Pontos mínimos para o Top 3 resgatar.", synced: true);

            _cfgRewardTop3Label = _rulesConfig.BindConfig("Rewards", "RewardTop3Label",
                ReadLegacyString(legacyOverrides, "RewardTop3Label", defaults.RewardTop3Label),
                "Texto exibido para a recompensa do Top 3.", synced: true);

            _cfgRewardTop3Prefab = _rulesConfig.BindConfig("Rewards", "RewardTop3Prefab",
                ReadLegacyString(legacyOverrides, "RewardTop3Prefab", defaults.RewardTop3Prefab),
                "Prefab do item entregue ao Top 3.", synced: true);

            _cfgRewardTop3Amount = _rulesConfig.BindConfig("Rewards", "RewardTop3Amount",
                ReadLegacyInt(legacyOverrides, "RewardTop3Amount", defaults.RewardTop3Amount),
                "Quantidade do item entregue ao Top 3.", synced: true);

            _cfgPointsExchangeEnabled = _rulesConfig.BindConfig("PointsExchange", "Enabled",
                ReadLegacyBool(legacyOverrides, "PointsExchangeEnabled", defaults.PointsExchangeEnabled),
                "Ativa o botão de câmbio de pontos por moedas no HUD.", synced: true);

            _cfgPointsExchangePrefab = _rulesConfig.BindConfig("PointsExchange", "Prefab",
                ReadLegacyString(legacyOverrides, "PointsExchangePrefab", defaults.PointsExchangePrefab),
                "Prefab entregue no câmbio de pontos. Exemplo: Coins.", synced: true);

            _cfgPointsExchangeCoinsPerPoint = _rulesConfig.BindConfig("PointsExchange", "CoinsPerPoint",
                ReadLegacyInt(legacyOverrides, "PointsExchangeCoinsPerPoint", defaults.PointsExchangeCoinsPerPoint),
                "Quantidade de moedas entregues por cada ponto trocado.", synced: true);

            _cfgPointsExchangeMinPoints = _rulesConfig.BindConfig("PointsExchange", "MinPoints",
                ReadLegacyInt(legacyOverrides, "PointsExchangeMinPoints", defaults.PointsExchangeMinPoints),
                "Valor legado. O câmbio não exige requisito mínimo; basta o jogador ter pontos disponíveis.", synced: true);

            _cfgPointsExchangeMaxPointsPerRequest = _rulesConfig.BindConfig("PointsExchange", "MaxPointsPerRequest",
                ReadLegacyInt(legacyOverrides, "PointsExchangeMaxPointsPerRequest", defaults.PointsExchangeMaxPointsPerRequest),
                "Máximo de pontos convertidos por clique. Use 0 para trocar todos.", synced: true);

            _cfgMarketplaceQuestPointMap = _rulesConfig.BindConfig("MarketplaceQuestPoints", "QuestPointMap",
                BuildMarketplaceQuestPointMapDefault(legacyOverrides),
                "Formato: questId:pontos separados por vírgula. Ex: ccq_b21:450,ccq_b22:150", synced: true);

            _cfgDeathPenaltyRules = _rulesConfig.BindConfig("DeathPenalty", "Rules",
                ReadLegacyString(legacyOverrides, "DeathPenalty.Rules", ReadLegacyString(legacyOverrides, "DeathPenaltyRules", "1:0,2:100,5:300,10:800")),
                "Penalidade por morte em marcos. Formato: mortes:pontosPerdidos,mortes:pontosPerdidos. Exemplo: 1:0,2:100,5:300,10:800", synced: true);

            _cfgDeathPenaltyUseMultiplier = _rulesConfig.BindConfig("DeathPenalty", "UseMultiplierMode",
                ReadLegacyBool(legacyOverrides, "DeathPenalty.UseMultiplierMode", ReadLegacyBool(legacyOverrides, "DeathPenaltyUseMultiplier", false)),
                "Se true, ignora a tabela Rules e aplica PenaltyPerDeath a cada morte. Exemplo: 15 mortes x 100 = -1500.", synced: true);

            _cfgDeathPenaltyPerDeath = _rulesConfig.BindConfig("DeathPenalty", "PenaltyPerDeath",
                Mathf.Max(0, ReadLegacyInt(legacyOverrides, "DeathPenalty.PenaltyPerDeath", ReadLegacyInt(legacyOverrides, "DeathPenaltyPerDeath", 100))),
                "Valor perdido por cada morte quando UseMultiplierMode=true.", synced: true);

            _cfgEnableExplorationJackpots = _rulesConfig.BindConfig("ExplorationMapJackpots", "Enabled",
                ReadLegacyBool(legacyOverrides, "ExplorationMapJackpots.Enabled", true),
                "Ativa jackpots por porcentagem de mapa revelado.", synced: true);

            _cfgExplorationMapJackpotRules = _rulesConfig.BindConfig("ExplorationMapJackpots", "Rules",
                ReadLegacyString(legacyOverrides, "ExplorationMapJackpots.Rules", ReadLegacyString(legacyOverrides, "ExplorationMapJackpotRules", "5:25;10:50;25:150;50:400;75:800;100:1500")),
                "Jackpots por porcentagem do mapa revelado. Formato: porcentagem:pontos;porcentagem:pontos. Exemplo: 5:25;10:50;25:150", synced: true);


            BindCombatBiomeRuleEntries(legacyOverrides);

            _cfgProductionCategoryRules = _rulesConfig.BindConfig("HudCategories", "Production",
                ReadLegacyString(legacyOverrides, "HudCategories.Production", DefaultProductionCategoryRules),
                "Producao/Conquistas por categoria COM pontos. O nome da categoria do config vira a aba do HUD. Formato obrigatorio: Categoria:Prefab=pontos,Prefab=pontos;Outra:Prefab=pontos. Exemplo: Armas:SwordIron=800;Armaduras:HelmetBronze=800;Comidas:DeerStew=25. Tambem organiza FarmJackpots e UniqueCraftJackpots.", synced: true);


            BindPointSectionEntries("BossPoints", "Pontos por bosses e minibosses.", legacyOverrides, _cfgBossPointEntries);
            BindPointSectionEntries("FishingPoints", "Pontos por peixe pescado.", legacyOverrides, _cfgFishingPointEntries);

            BindStringPointSectionEntries("SkillMilestoneJackpotPoints", "Pontos únicos por marco de skill. Formato: 20:500,40:1000,60:2000,80:3000,100:4000.", legacyOverrides, _cfgSkillMilestoneJackpotPointEntries);

            _cfgFarmJackpotRules = _rulesConfig.BindConfig("FarmJackpots", "Rules",
                ReadGroupedRulesDefault(legacyOverrides, "FarmJackpots", "Carrot:200:1000;Turnip:200:1200;Onion:200:1500;Barley:500:2000;Flax:500:2000"),
                "Jackpots de colheita. Edite somente esta linha. Formato: Prefab:quantidade:pontos;Prefab:quantidade:pontos. Exemplo: Carrot:200:1000;Barley:500:2000.", synced: true);

            _cfgUniqueCraftJackpotRules = _rulesConfig.BindConfig("UniqueCraftJackpots", "Rules",
                ReadGroupedRulesDefault(legacyOverrides, "UniqueCraftJackpots", "SwordIron:1:800;SwordSilver:1:1500;SwordBlackmetal:1:2500;ArmorWolfChest:1:2000;ArmorCarapaceChest:1:3500"),
                "Jackpot único por craft. Edite somente esta linha. Formato: Prefab:quantidade:pontos;Prefab:quantidade:pontos. Exemplo: SwordIron:1:800;ArmorWolfChest:1:2000.", synced: true);
        }

        private void BindCombatBiomeRuleEntries(Dictionary<string, string> legacyOverrides)
        {
            if (_cfgCombatBiomeRuleEntries == null)
                return;

            _cfgCombatBiomeRuleEntries.Clear();

            Dictionary<string, string> defaults = ParseCombatBiomeDefaults(DefaultCombatCategoryRules);

            BindSingleCombatBiome("Prados", defaults, legacyOverrides);
            BindSingleCombatBiome("Floresta Negra", defaults, legacyOverrides);
            BindSingleCombatBiome("Pântano", defaults, legacyOverrides);
            BindSingleCombatBiome("Montanha", defaults, legacyOverrides);
            BindSingleCombatBiome("Planícies", defaults, legacyOverrides);
            BindSingleCombatBiome("Oceano", defaults, legacyOverrides);
            BindSingleCombatBiome("Mistlands", defaults, legacyOverrides);
            BindSingleCombatBiome("Ashlands", defaults, legacyOverrides);
            BindSingleCombatBiome("Especiais", defaults, legacyOverrides);


        }

        private void BindSingleCombatBiome(string category, Dictionary<string, string> defaults, Dictionary<string, string> legacyOverrides)
        {
            string defaultValue;
            if (defaults == null || !defaults.TryGetValue(category, out defaultValue))
                defaultValue = string.Empty;

            string value = ReadLegacyString(legacyOverrides, "Combat." + category, defaultValue);

            _cfgCombatBiomeRuleEntries[category] = _rulesConfig.BindConfig(
                "Combat",
                category,
                value,
                "Mobs e pontos para esta aba do HUD. Formato: Prefab;Pontos,OutroPrefab;Pontos. Exemplo: Boar;2,Deer;3. Aceita qualquer prefab, inclusive de mods. O nome desta chave vira a categoria no HUD.",
                synced: true);
        }

        private Dictionary<string, string> ParseCombatBiomeDefaults(string raw)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string[] groups = raw.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string groupRaw in groups)
            {
                string group = groupRaw != null ? groupRaw.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(group))
                    continue;

                int colon = group.IndexOf(':');
                if (colon <= 0 || colon >= group.Length - 1)
                    continue;

                string category = NormalizeHudCategoryName(group.Substring(0, colon).Trim());
                string itemsRaw = group.Substring(colon + 1).Trim();
                if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(itemsRaw))
                    continue;

                List<string> items = new List<string>();
                string[] rawItems = itemsRaw.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string itemRaw in rawItems)
                {
                    string prefab;
                    int points;
                    if (TryParseCombatBiomeItem(itemRaw, out prefab, out points))
                        items.Add(prefab + ";" + Mathf.Max(0, points));
                }

                result[category] = string.Join(",", items.ToArray());
            }

            return result;
        }

        private string BuildCombatCategoryRulesFromBiomeEntries()
        {
            if (_cfgCombatBiomeRuleEntries == null || _cfgCombatBiomeRuleEntries.Count == 0)
                return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, ConfigEntry<string>> pair in _cfgCombatBiomeRuleEntries)
            {
                if (pair.Value == null)
                    continue;

                string category = NormalizeHudCategoryName(pair.Key);
                if (string.IsNullOrWhiteSpace(category))
                    continue;

                string convertedItems = ConvertCombatBiomeItemsToHudCategoryItems(pair.Value.Value);
                if (string.IsNullOrWhiteSpace(convertedItems))
                    continue;

                if (sb.Length > 0)
                    sb.Append(';');

                sb.Append(category);
                sb.Append(':');
                sb.Append(convertedItems);
            }

            return sb.ToString();
        }

        private string ConvertCombatBiomeItemsToHudCategoryItems(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            List<string> items = new List<string>();
            string[] entries = raw.Split(new[] { ',', '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entryRaw in entries)
            {
                string prefab;
                int points;
                if (TryParseCombatBiomeItem(entryRaw, out prefab, out points))
                    items.Add(prefab + ":" + Mathf.Max(0, points));
            }

            return string.Join(",", items.ToArray());
        }

        private bool TryParseCombatBiomeItem(string itemRaw, out string prefab, out int points)
        {
            prefab = string.Empty;
            points = 0;

            if (string.IsNullOrWhiteSpace(itemRaw))
                return false;

            string item = itemRaw.Trim();

            int splitIndex = item.LastIndexOf(';');
            if (splitIndex <= 0)
                splitIndex = item.LastIndexOf('=');
            if (splitIndex <= 0)
                splitIndex = item.LastIndexOf(':');

            if (splitIndex <= 0 || splitIndex >= item.Length - 1)
                return false;

            string prefabRaw = item.Substring(0, splitIndex).Trim();
            string pointsRaw = item.Substring(splitIndex + 1).Trim();

            int parsedPoints;
            if (!int.TryParse(pointsRaw, out parsedPoints))
                return false;

            prefab = SafeKey(prefabRaw);
            points = Mathf.Max(0, parsedPoints);

            return !string.IsNullOrWhiteSpace(prefab);
        }

        private void RewriteRulesConfigHeaderAndGroupedSections()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_rulesFilePath) || !File.Exists(_rulesFilePath))
                    return;

                string[] lines = File.ReadAllLines(_rulesFilePath, Encoding.UTF8);
                List<string> cleaned = new List<string>(lines.Length + 40);
                string currentSection = string.Empty;
                bool skippingGroupedSection = false;
                HashSet<string> groupedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "KillPoints",
                    "CraftPoints",
                    "FarmJackpots",
                    "UniqueCraftJackpots",
                    "DeathPenalty",
                    "ExplorationMapJackpots"
                };

                cleaned.Add("## Glitnir Ranking - Regras de pontuação");
                cleaned.Add("##");
                cleaned.Add("## Edite este arquivo no servidor. As regras marcadas como synced são enviadas aos clientes.");
                cleaned.Add("##");
                cleaned.Add("## Combate fica em [Combat], uma chave por bioma/categoria, exatamente igual ao HUD.");
                cleaned.Add("##   [Combat]");
                cleaned.Add("##   Prados = Boar;2,Deer;3");
                cleaned.Add("##   Floresta Negra = Troll;20,Greydwarf;1");
                cleaned.Add("##   Pântano = Draugr;2,Blob;2");
                cleaned.Add("##   [UniqueCraftJackpots]     Rules = Prefab:quantidade:pontos;Prefab:quantidade:pontos");
                cleaned.Add("##   [DeathPenalty]            Rules = mortes:pontosPerdidos,mortes:pontosPerdidos");
                cleaned.Add("##   [ExplorationMapJackpots]  Rules = porcentagem:pontos;porcentagem:pontos");
                cleaned.Add("##");
                cleaned.Add("## Exemplos de prefabs reais comuns:");
                cleaned.Add("##   Carrot:200:1000");
                cleaned.Add("##   SwordIron:1:800");
                cleaned.Add("##   ArmorWolfChest:1:2000");
                cleaned.Add("##   1:0,2:100,5:300,10:800");
                cleaned.Add("##");
                cleaned.Add("## Importante: se um item não pontuar, ative DebugLogging/LogPointsChanges e veja no log");
                cleaned.Add("## o prefab detectado pelo ranking no momento do kill, craft ou colheita.");
                cleaned.Add("");

                bool insertedFarm = false;
                bool insertedUnique = false;
                bool insertedDeath = false;
                bool insertedExploration = false;

                foreach (string rawLine in lines)
                {
                    string line = rawLine != null ? rawLine.Trim() : string.Empty;

                    if (line.StartsWith("## Glitnir Ranking - Regras de pontuação", StringComparison.OrdinalIgnoreCase))
                        continue;


                    if (string.Equals(currentSection, "HudCategories", StringComparison.OrdinalIgnoreCase) &&
                        line.StartsWith("Combat", StringComparison.OrdinalIgnoreCase) &&
                        line.IndexOf('=') > 0)
                    {
                        continue;
                    }

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string nextSection = line.Substring(1, line.Length - 2).Trim();
                        skippingGroupedSection = groupedSections.Contains(nextSection);
                        currentSection = nextSection;

                        if (skippingGroupedSection)
                        {
                            if (nextSection.Equals("FarmJackpots", StringComparison.OrdinalIgnoreCase) && !insertedFarm)
                            {
                                AppendGroupedFarmSection(cleaned);
                                insertedFarm = true;
                            }
                            else if (nextSection.Equals("UniqueCraftJackpots", StringComparison.OrdinalIgnoreCase) && !insertedUnique)
                            {
                                AppendGroupedUniqueCraftSection(cleaned);
                                insertedUnique = true;
                            }
                            else if (nextSection.Equals("DeathPenalty", StringComparison.OrdinalIgnoreCase) && !insertedDeath)
                            {
                                AppendGroupedDeathPenaltySection(cleaned);
                                insertedDeath = true;
                            }
                            else if (nextSection.Equals("ExplorationMapJackpots", StringComparison.OrdinalIgnoreCase) && !insertedExploration)
                            {
                                AppendGroupedExplorationMapSection(cleaned);
                                insertedExploration = true;
                            }
                            continue;
                        }

                        cleaned.Add(rawLine);
                        continue;
                    }

                    if (skippingGroupedSection)
                        continue;

                    cleaned.Add(rawLine);
                }

                if (!insertedFarm)
                    AppendGroupedFarmSection(cleaned);
                if (!insertedUnique)
                    AppendGroupedUniqueCraftSection(cleaned);
                if (!insertedDeath)
                    AppendGroupedDeathPenaltySection(cleaned);
                if (!insertedExploration)
                    AppendGroupedExplorationMapSection(cleaned);

                File.WriteAllLines(_rulesFilePath, cleaned.ToArray(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Falha ao organizar config do ranking: " + ex.Message);
            }
        }

        private void AppendGroupedFarmSection(List<string> lines)
        {
            lines.Add("");
            lines.Add("[FarmJackpots]");
            lines.Add("");
            lines.Add("## Jackpot de colheita por prefab configurado.");
            lines.Add("## Formato: Prefab:quantidade:pontos;Prefab:quantidade:pontos");
            lines.Add("## Exemplo: Carrot:200:1000;Barley:500:2000");
            lines.Add("Rules = " + (_cfgFarmJackpotRules != null ? SanitizeDelimitedJackpotRules(_cfgFarmJackpotRules.Value) : "Carrot:200:1000;Barley:500:2000"));
        }

        private void AppendGroupedUniqueCraftSection(List<string> lines)
        {
            lines.Add("");
            lines.Add("[UniqueCraftJackpots]");
            lines.Add("");
            lines.Add("## Jackpot único por craft de qualquer prefab configurado.");
            lines.Add("## Formato: Prefab:quantidade:pontos;Prefab:quantidade:pontos");
            lines.Add("## Exemplo: SwordIron:1:800;ArmorWolfChest:1:2000");
            lines.Add("Rules = " + (_cfgUniqueCraftJackpotRules != null ? SanitizeDelimitedJackpotRules(_cfgUniqueCraftJackpotRules.Value) : "SwordIron:1:800;ArmorWolfChest:1:2000"));
        }

        private void AppendGroupedDeathPenaltySection(List<string> lines)
        {
            lines.Add("");
            lines.Add("[DeathPenalty]");
            lines.Add("");
            lines.Add("## Penalidade por morte.");
            lines.Add("## UseMultiplierMode=false usa a tabela Rules abaixo.");
            lines.Add("## UseMultiplierMode=true ignora Rules e aplica PenaltyPerDeath para cada morte.");
            lines.Add("## Exemplo multiplicador: PenaltyPerDeath=100 e 15 mortes = -1500 pontos.");
            lines.Add("UseMultiplierMode = " + (_cfgDeathPenaltyUseMultiplier != null ? _cfgDeathPenaltyUseMultiplier.Value.ToString() : "false"));
            lines.Add("PenaltyPerDeath = " + (_cfgDeathPenaltyPerDeath != null ? Mathf.Max(0, _cfgDeathPenaltyPerDeath.Value).ToString() : "100"));
            lines.Add("");
            lines.Add("## Formato do modo tabela: mortes:pontosPerdidos,mortes:pontosPerdidos");
            lines.Add("## Exemplo: 1:0,2:100,5:300,10:800");
            lines.Add("Rules = " + (_cfgDeathPenaltyRules != null ? SanitizeDeathPenaltyRules(_cfgDeathPenaltyRules.Value) : "1:0,2:100,5:300,10:800"));
        }

        private void AppendGroupedExplorationMapSection(List<string> lines)
        {
            lines.Add("");
            lines.Add("[ExplorationMapJackpots]");
            lines.Add("");
            lines.Add("## Jackpot por porcentagem de mapa revelado.");
            lines.Add("## Formato: porcentagem:pontos;porcentagem:pontos");
            lines.Add("## Exemplo: 5:25;10:50;25:150;50:400;75:800;100:1500");
            lines.Add("Enabled = " + (_cfgEnableExplorationJackpots != null ? _cfgEnableExplorationJackpots.Value.ToString() : "true"));
            lines.Add("Rules = " + (_cfgExplorationMapJackpotRules != null ? _cfgExplorationMapJackpotRules.Value : "5:25;10:50;25:150;50:400;75:800;100:1500"));
        }

        private void CleanupFishKillPointConfigEntries()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_rulesFilePath) || !File.Exists(_rulesFilePath))
                    return;

                string[] lines = File.ReadAllLines(_rulesFilePath, Encoding.UTF8);
                List<string> cleaned = new List<string>(lines.Length);
                bool inKillPointsSection = false;
                bool changed = false;

                foreach (string rawLine in lines)
                {
                    string trimmed = rawLine != null ? rawLine.Trim() : string.Empty;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                        inKillPointsSection = trimmed.Equals("[KillPoints]", StringComparison.OrdinalIgnoreCase);

                    int eqIndex = trimmed.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        string key = trimmed.Substring(0, eqIndex).Trim();
                        string prefabKey = key;

                        if (key.StartsWith("KillPoints.", StringComparison.OrdinalIgnoreCase))
                            prefabKey = key.Substring("KillPoints.".Length).Trim();
                        else if (!inKillPointsSection)
                            prefabKey = null;

                        if (!string.IsNullOrWhiteSpace(prefabKey) && IsFishPrefab(prefabKey))
                        {
                            changed = true;
                            continue;
                        }
                    }

                    cleaned.Add(rawLine);
                }

                if (changed)
                {
                    File.WriteAllLines(_rulesFilePath, cleaned.ToArray(), Encoding.UTF8);

                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Falha ao limpar prefabs de peixe do config: " + ex.Message);
            }
        }

        private void CleanupGroupedProductionConfigEntries()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_rulesFilePath) || !File.Exists(_rulesFilePath))
                    return;

                string[] lines = File.ReadAllLines(_rulesFilePath, Encoding.UTF8);
                List<string> cleaned = new List<string>(lines.Length);
                string currentSection = string.Empty;
                bool changed = false;
                HashSet<string> groupedSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "KillPoints",
                    "CraftPoints",
                    "FarmJackpots",
                    "UniqueCraftJackpots",
                    "DeathPenalty",
                    "ExplorationMapJackpots"
                };

                foreach (string rawLine in lines)
                {
                    string line = rawLine != null ? rawLine.Trim() : string.Empty;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        currentSection = line.Substring(1, line.Length - 2).Trim();
                        cleaned.Add(rawLine);
                        continue;
                    }

                    int eqIndex = line.IndexOf('=');
                    if (eqIndex > 0 && groupedSections.Contains(currentSection))
                    {
                        string key = line.Substring(0, eqIndex).Trim();


                        bool allowedKey = key.Equals("Rules", StringComparison.OrdinalIgnoreCase);

                        if (currentSection.Equals("DeathPenalty", StringComparison.OrdinalIgnoreCase))
                        {
                            allowedKey =
                                allowedKey ||
                                key.Equals("UseMultiplierMode", StringComparison.OrdinalIgnoreCase) ||
                                key.Equals("PenaltyPerDeath", StringComparison.OrdinalIgnoreCase);
                        }
                        else if (currentSection.Equals("ExplorationMapJackpots", StringComparison.OrdinalIgnoreCase))
                        {
                            allowedKey = allowedKey || key.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
                        }

                        if (!allowedKey)
                        {
                            changed = true;
                            continue;
                        }
                    }

                    cleaned.Add(rawLine);
                }

                if (changed)
                {
                    File.WriteAllLines(_rulesFilePath, cleaned.ToArray(), Encoding.UTF8);

                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Falha ao reorganizar config de produção: " + ex.Message);
            }
        }

        private string BuildMarketplaceQuestPointMapDefault(Dictionary<string, string> legacyOverrides)
        {
            string directLegacy = ReadLegacyString(legacyOverrides, "MarketplaceQuestPoints.QuestPointMap", null);
            if (!string.IsNullOrWhiteSpace(directLegacy))
                return SanitizeMarketplaceQuestPointMap(directLegacy);

            directLegacy = ReadLegacyString(legacyOverrides, "QuestPointMap", null);
            if (!string.IsNullOrWhiteSpace(directLegacy))
                return SanitizeMarketplaceQuestPointMap(directLegacy);

            Dictionary<string, int> merged = GetMergedDefaultPointEntries("MarketplaceQuestPoints", legacyOverrides);
            if (merged.Count <= 0)
                return "ccq_b21:450,ccq_b22:150";

            return string.Join(",", merged
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => SafeMarketplaceQuestKey(p.Key) + ":" + Mathf.Max(0, p.Value)));
        }

        private Dictionary<string, int> ParseMarketplaceQuestPointMap(string raw)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string[] entries = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entryRaw in entries)
            {
                string entry = entryRaw != null ? entryRaw.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] parts = entry.Split(new[] { ':' }, 2, StringSplitOptions.None);
                if (parts.Length != 2)
                    continue;

                string questKey = SafeMarketplaceQuestKey(parts[0]);
                if (string.IsNullOrWhiteSpace(questKey))
                    continue;

                int points;
                if (!int.TryParse(parts[1].Trim(), out points))
                    continue;

                result[questKey] = points;
            }

            return result;
        }

        private string SanitizeMarketplaceQuestPointMap(string raw)
        {
            Dictionary<string, int> parsed = ParseMarketplaceQuestPointMap(raw);
            if (parsed.Count <= 0)
                return string.Empty;

            return string.Join(",", parsed
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Key + ":" + Mathf.Max(0, p.Value)));
        }

        private Dictionary<int, int> ParseSkillMilestoneMap(string raw)
        {
            Dictionary<int, int> result = new Dictionary<int, int>();

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string[] entries = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entryRaw in entries)
            {
                string entry = entryRaw != null ? entryRaw.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] parts = entry.Split(new[] { ':' }, 2, StringSplitOptions.None);
                if (parts.Length != 2)
                    continue;

                int level;
                int points;
                if (!int.TryParse(parts[0].Trim(), out level))
                    continue;
                if (!int.TryParse(parts[1].Trim(), out points))
                    continue;

                level = Mathf.Clamp(level, 1, 100);
                result[level] = Mathf.Max(0, points);
            }

            return result;
        }

        private string SanitizeSkillMilestoneMap(string raw)
        {
            Dictionary<int, int> parsed = ParseSkillMilestoneMap(raw);
            if (parsed.Count <= 0)
                return string.Empty;

            return string.Join(",", parsed
                .OrderBy(p => p.Key)
                .Select(p => p.Key + ":" + Mathf.Max(0, p.Value)));
        }

        private JackpotRule ParseJackpotRule(string raw)
        {
            JackpotRule result = new JackpotRule();

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string[] parts = raw.Split(new[] { ':' }, 2, StringSplitOptions.None);
            if (parts.Length != 2)
                return result;

            int amount;
            int points;
            if (!int.TryParse(parts[0].Trim(), out amount))
                return result;
            if (!int.TryParse(parts[1].Trim(), out points))
                return result;

            result.RequiredAmount = Mathf.Max(1, amount);
            result.Points = Mathf.Max(0, points);
            return result;
        }

        private string SanitizeJackpotRule(string raw)
        {
            JackpotRule rule = ParseJackpotRule(raw);
            if (rule == null || rule.RequiredAmount <= 0)
                return "1:0";

            return rule.RequiredAmount + ":" + Mathf.Max(0, rule.Points);
        }

        private string StripHudCategoryPointPart(string itemRaw)
        {
            if (string.IsNullOrWhiteSpace(itemRaw))
                return "";

            string item = itemRaw.Trim();

            int eqIndex = item.IndexOf('=');
            if (eqIndex > 0)
                item = item.Substring(0, eqIndex).Trim();


            int lastColon = item.LastIndexOf(':');
            if (lastColon > 0 && lastColon < item.Length - 1)
            {
                string possiblePoints = item.Substring(lastColon + 1).Trim();
                int points;
                if (int.TryParse(possiblePoints, out points))
                    item = item.Substring(0, lastColon).Trim();
            }

            return SafeKey(item);
        }

        private bool TryParseHudCategoryPointItem(string itemRaw, out string prefab, out int points)
        {
            prefab = "";
            points = 0;

            if (string.IsNullOrWhiteSpace(itemRaw))
                return false;

            string item = itemRaw.Trim();
            int splitIndex = item.LastIndexOf('=');

            if (splitIndex <= 0)
                splitIndex = item.LastIndexOf(':');

            if (splitIndex <= 0 || splitIndex >= item.Length - 1)
                return false;

            string prefabRaw = item.Substring(0, splitIndex).Trim();
            string pointsRaw = item.Substring(splitIndex + 1).Trim();

            int parsedPoints;
            if (!int.TryParse(pointsRaw, out parsedPoints))
                return false;

            prefab = SafeKey(prefabRaw);
            points = Mathf.Max(0, parsedPoints);

            return !string.IsNullOrWhiteSpace(prefab);
        }

        private Dictionary<string, string> ParseHudCategoryRules(string raw)
        {
            Dictionary<string, string> categories;
            ParseHudCategorizedPointRules(raw, out categories);
            return categories;
        }

        private static string NormalizeHudCategoryName(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return category;

            string c = category.Trim();


            if (c.Equals("Arcos e Munições", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Arcos e Municoes", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Armas, Armaduras e Munições", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Armas, Armaduras e Municoes", StringComparison.OrdinalIgnoreCase))
                return "Armas";

            if (c.Equals("Comidas e Consumíveis", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Comidas e Consumiveis", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("Comidas e bebidas", StringComparison.OrdinalIgnoreCase))
                return "Comidas";

            return c;
        }

        private Dictionary<string, int> ParseHudCategorizedPointRules(string raw, out Dictionary<string, string> categories)
        {
            categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> points = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return points;

            string[] groups = raw.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string groupRaw in groups)
            {
                string group = groupRaw != null ? groupRaw.Trim() : "";
                if (string.IsNullOrWhiteSpace(group))
                    continue;

                int colon = group.IndexOf(':');
                if (colon <= 0 || colon >= group.Length - 1)
                    continue;

                string category = NormalizeHudCategoryName(group.Substring(0, colon).Trim());
                if (string.IsNullOrWhiteSpace(category))
                    continue;

                string itemsRaw = group.Substring(colon + 1);
                string[] items = itemsRaw.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string itemRaw in items)
                {
                    string prefabForCategory = StripHudCategoryPointPart(itemRaw);
                    if (string.IsNullOrWhiteSpace(prefabForCategory))
                        continue;

                    categories[prefabForCategory] = category;

                    string prefabForPoints;
                    int itemPoints;
                    if (TryParseHudCategoryPointItem(itemRaw, out prefabForPoints, out itemPoints))
                        points[prefabForPoints] = itemPoints;
                }
            }

            return points;
        }

        private Dictionary<string, int> ParseDelimitedPointRules(string raw)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string[] entries = raw.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entryRaw in entries)
            {
                string entry = entryRaw != null ? entryRaw.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] parts = entry.Split(new[] { ':' }, 2, StringSplitOptions.None);
                if (parts.Length != 2)
                    continue;

                string prefab = SafeKey(parts[0]);
                if (string.IsNullOrWhiteSpace(prefab))
                    continue;

                int points;
                if (!int.TryParse(parts[1].Trim(), out points))
                    continue;

                result[prefab] = Mathf.Max(0, points);
            }

            return result;
        }

        private Dictionary<string, JackpotRule> ParseDelimitedJackpotRules(string raw)
        {
            Dictionary<string, JackpotRule> result = new Dictionary<string, JackpotRule>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string[] entries = raw.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entryRaw in entries)
            {
                string entry = entryRaw != null ? entryRaw.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] parts = entry.Split(new[] { ':' }, 3, StringSplitOptions.None);
                if (parts.Length != 3)
                    continue;

                string prefab = SafeKey(parts[0]);
                if (string.IsNullOrWhiteSpace(prefab))
                    continue;

                int amount;
                int points;
                if (!int.TryParse(parts[1].Trim(), out amount))
                    continue;
                if (!int.TryParse(parts[2].Trim(), out points))
                    continue;

                result[prefab] = new JackpotRule
                {
                    RequiredAmount = Mathf.Max(1, amount),
                    Points = Mathf.Max(0, points)
                };
            }

            return result;
        }

        private Dictionary<int, int> ParseDeathPenaltyRules(string raw)
        {
            Dictionary<int, int> result = new Dictionary<int, int>();

            if (string.IsNullOrWhiteSpace(raw))
                return result;

            string[] entries = raw.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entryRaw in entries)
            {
                string entry = entryRaw != null ? entryRaw.Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string[] parts = entry.Split(new[] { ':' }, 2, StringSplitOptions.None);
                if (parts.Length != 2)
                    continue;

                int deaths;
                int pointsLost;
                if (!int.TryParse(parts[0].Trim(), out deaths))
                    continue;
                if (!int.TryParse(parts[1].Trim(), out pointsLost))
                    continue;

                deaths = Mathf.Max(1, deaths);
                pointsLost = Mathf.Max(0, pointsLost);
                result[deaths] = pointsLost;
            }

            return result;
        }

        private string SanitizeDeathPenaltyRules(string raw)
        {
            Dictionary<int, int> parsed = ParseDeathPenaltyRules(raw);
            if (parsed.Count <= 0)
                return string.Empty;

            return string.Join(",", parsed
                .OrderBy(p => p.Key)
                .Select(p => Mathf.Max(1, p.Key) + ":" + Mathf.Max(0, p.Value)));
        }

        private string SanitizeDelimitedPointRules(string raw)
        {
            return SanitizeDelimitedPointRules(raw, ";");
        }

        private string SanitizeDelimitedPointRules(string raw, string delimiter)
        {
            Dictionary<string, int> parsed = ParseDelimitedPointRules(raw);
            if (parsed.Count <= 0)
                return string.Empty;

            if (string.IsNullOrEmpty(delimiter))
                delimiter = ";";

            return string.Join(delimiter, parsed
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Where(p => !IsFishPrefab(p.Key))
                .Select(p => p.Key + ":" + Mathf.Max(0, p.Value)));
        }

        private string SanitizeDelimitedJackpotRules(string raw)
        {
            Dictionary<string, JackpotRule> parsed = ParseDelimitedJackpotRules(raw);
            if (parsed.Count <= 0)
                return string.Empty;

            return string.Join(";", parsed
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.Key + ":" + Mathf.Max(1, p.Value.RequiredAmount) + ":" + Mathf.Max(0, p.Value.Points)));
        }

        private string ReadGroupedRulesDefault(Dictionary<string, string> legacyOverrides, string section, string fallback)
        {
            string direct = ReadLegacyString(legacyOverrides, section + ".Rules", null);
            if (!string.IsNullOrWhiteSpace(direct))
                return direct.Trim();

            direct = ReadLegacyString(legacyOverrides, "Rules", null);
            if (!string.IsNullOrWhiteSpace(direct))
                return direct.Trim();

            return fallback;
        }

        private string BuildDelimitedPointRulesDefault(string section, Dictionary<string, string> legacyOverrides, string fallback)
        {
            string direct = ReadLegacyString(legacyOverrides, section + ".Rules", null);
            if (!string.IsNullOrWhiteSpace(direct))
                return SanitizeDelimitedPointRules(direct);

            Dictionary<string, int> merged = GetMergedDefaultPointEntries(section, legacyOverrides);
            if (merged.Count <= 0)
                return fallback;

            return string.Join(";", merged
                .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p => SafeKey(p.Key) + ":" + Mathf.Max(0, p.Value)));
        }

        private string BuildDelimitedJackpotRulesDefault(string section, Dictionary<string, string> legacyOverrides, string fallback)
        {
            string direct = ReadLegacyString(legacyOverrides, section + ".Rules", null);
            if (!string.IsNullOrWhiteSpace(direct))
                return SanitizeDelimitedJackpotRules(direct);

            Dictionary<string, string> merged = GetMergedDefaultStringEntries(section, legacyOverrides);
            if (merged.Count <= 0)
                return fallback;

            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string> pair in merged.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (pair.Key.Equals("Rules", StringComparison.OrdinalIgnoreCase))
                    continue;

                JackpotRule rule = ParseJackpotRule(pair.Value);
                if (rule == null || rule.RequiredAmount <= 0)
                    continue;

                parts.Add(SafeKey(pair.Key) + ":" + Mathf.Max(1, rule.RequiredAmount) + ":" + Mathf.Max(0, rule.Points));
            }

            return parts.Count > 0 ? string.Join(";", parts) : fallback;
        }

        private void BindPointSectionEntries(
            string prefix,
            string descriptionPrefix,
            Dictionary<string, string> legacyOverrides,
            Dictionary<string, ConfigEntry<int>> target)
        {
            Dictionary<string, int> mergedDefaults = GetMergedDefaultPointEntries(prefix, legacyOverrides);
            List<string> existingKeys = new List<string>(target.Keys);

            foreach (string existingKey in existingKeys)
            {
                if (!mergedDefaults.ContainsKey(existingKey))
                    mergedDefaults[existingKey] = target[existingKey].Value;
            }

            foreach (KeyValuePair<string, int> pair in mergedDefaults.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                string cleanKey = SafeKey(pair.Key);
                if (string.IsNullOrWhiteSpace(cleanKey))
                    continue;
                if (prefix.Equals("KillPoints", StringComparison.OrdinalIgnoreCase) && IsFishPrefab(cleanKey))
                    continue;
                if (prefix.Equals("FishingPoints", StringComparison.OrdinalIgnoreCase) && !IsFishPrefab(cleanKey))
                    continue;

                ConfigEntry<int> entry;
                if (!target.TryGetValue(cleanKey, out entry))
                {
                    entry = _rulesConfig.BindConfig(prefix, cleanKey, pair.Value,
                        descriptionPrefix + " Chave: " + cleanKey, synced: true);
                    target[cleanKey] = entry;
                }
            }
        }

        private void BindStringPointSectionEntries(
            string prefix,
            string descriptionPrefix,
            Dictionary<string, string> legacyOverrides,
            Dictionary<string, ConfigEntry<string>> target)
        {
            Dictionary<string, string> mergedDefaults = GetMergedDefaultStringEntries(prefix, legacyOverrides);
            List<string> existingKeys = new List<string>(target.Keys);

            foreach (string existingKey in existingKeys)
            {
                if (!mergedDefaults.ContainsKey(existingKey))
                    mergedDefaults[existingKey] = target[existingKey].Value;
            }

            foreach (KeyValuePair<string, string> pair in mergedDefaults.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                string cleanKey = SafeKey(pair.Key);
                if (string.IsNullOrWhiteSpace(cleanKey))
                    continue;

                ConfigEntry<string> entry;
                if (!target.TryGetValue(cleanKey, out entry))
                {
                    entry = _rulesConfig.BindConfig(prefix, cleanKey, SanitizeSkillMilestoneMap(pair.Value),
                        descriptionPrefix + " Chave: " + cleanKey, synced: true);
                    target[cleanKey] = entry;
                }
            }
        }

        private void BindGenericJackpotSectionEntries(
            string prefix,
            string descriptionPrefix,
            Dictionary<string, string> legacyOverrides,
            Dictionary<string, ConfigEntry<string>> target)
        {
            Dictionary<string, string> mergedDefaults = GetMergedDefaultStringEntries(prefix, legacyOverrides);
            List<string> existingKeys = new List<string>(target.Keys);

            foreach (string existingKey in existingKeys)
            {
                if (!mergedDefaults.ContainsKey(existingKey))
                    mergedDefaults[existingKey] = target[existingKey].Value;
            }

            foreach (KeyValuePair<string, string> pair in mergedDefaults.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                string cleanKey = SafeKey(pair.Key);
                if (string.IsNullOrWhiteSpace(cleanKey))
                    continue;

                ConfigEntry<string> entry;
                if (!target.TryGetValue(cleanKey, out entry))
                {
                    entry = _rulesConfig.BindConfig(prefix, cleanKey, SanitizeJackpotRule(pair.Value),
                        descriptionPrefix + " Chave: " + cleanKey, synced: true);
                    target[cleanKey] = entry;
                }
            }
        }

        private Dictionary<string, string> GetMergedDefaultStringEntries(string prefix, Dictionary<string, string> legacyOverrides)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in ParseRuleTemplate(BuildDefaultRulesFileContents()))
            {
                if (!pair.Key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                    continue;

                string cleanKey = SafeKey(pair.Key.Substring(prefix.Length + 1));
                if (string.IsNullOrWhiteSpace(cleanKey))
                    continue;

                result[cleanKey] = pair.Value;
            }

            foreach (KeyValuePair<string, string> pair in legacyOverrides)
            {
                if (!pair.Key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                    continue;

                string cleanKey = SafeKey(pair.Key.Substring(prefix.Length + 1));
                if (string.IsNullOrWhiteSpace(cleanKey))
                    continue;

                result[cleanKey] = pair.Value;
            }

            return result;
        }

        private Dictionary<string, int> GetMergedDefaultPointEntries(string prefix, Dictionary<string, string> legacyOverrides)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in ParseRuleTemplate(BuildDefaultRulesFileContents()))
            {
                if (!pair.Key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                    continue;

                string cleanKey = SafeKey(pair.Key.Substring(prefix.Length + 1));
                if (string.IsNullOrWhiteSpace(cleanKey))
                    continue;
                if (prefix.Equals("KillPoints", StringComparison.OrdinalIgnoreCase) && IsFishPrefab(cleanKey))
                    continue;
                if (prefix.Equals("FishingPoints", StringComparison.OrdinalIgnoreCase) && !IsFishPrefab(cleanKey))
                    continue;

                int value;
                if (!int.TryParse(pair.Value, out value))
                    value = 0;

                result[cleanKey] = value;
            }

            foreach (KeyValuePair<string, string> pair in legacyOverrides)
            {
                if (!pair.Key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
                    continue;

                string cleanKey = SafeKey(pair.Key.Substring(prefix.Length + 1));
                if (string.IsNullOrWhiteSpace(cleanKey))
                    continue;
                if (prefix.Equals("KillPoints", StringComparison.OrdinalIgnoreCase) && IsFishPrefab(cleanKey))
                    continue;
                if (prefix.Equals("FishingPoints", StringComparison.OrdinalIgnoreCase) && !IsFishPrefab(cleanKey))
                    continue;

                int value;
                if (!int.TryParse(pair.Value, out value))
                    continue;

                result[cleanKey] = value;
            }

            return result;
        }

        private Dictionary<string, string> ParseRuleTemplate(string contents)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(contents))
                return result;

            using (StringReader reader = new StringReader(contents))
            {
                string rawLine;
                while ((rawLine = reader.ReadLine()) != null)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (line.StartsWith("#") || line.StartsWith(";") || line.StartsWith("//"))
                        continue;

                    int eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0)
                        continue;

                    string key = line.Substring(0, eqIndex).Trim();
                    string value = line.Substring(eqIndex + 1).Trim();

                    if (!string.IsNullOrWhiteSpace(key))
                        result[key] = value;
                }
            }

            return result;
        }

        private bool ReadLegacyBool(Dictionary<string, string> values, string key, bool fallback)
        {
            string raw;
            return values != null && values.TryGetValue(key, out raw) ? ParseBool(raw, fallback) : fallback;
        }

        private int ReadLegacyInt(Dictionary<string, string> values, string key, int fallback)
        {
            string raw;
            return values != null && values.TryGetValue(key, out raw) ? ParseInt(raw, fallback) : fallback;
        }

        private string ReadLegacyString(Dictionary<string, string> values, string key, string fallback)
        {
            string raw;
            if (values != null && values.TryGetValue(key, out raw) && !string.IsNullOrWhiteSpace(raw))
                return raw;

            return fallback;
        }

        private void ApplyRulesFromSyncedConfig()
        {
            if (_rulesConfig == null)
                return;

            RankingRules rules = new RankingRules();

            if (_cfgRankingEnabled != null) rules.RankingEnabled = _cfgRankingEnabled.Value;
            if (_cfgEnableKillPoints != null) rules.EnableKillPoints = _cfgEnableKillPoints.Value;
            if (_cfgEnableBossPoints != null) rules.EnableBossPoints = _cfgEnableBossPoints.Value;
            if (_cfgDefaultKillPoints != null) rules.DefaultKillPoints = Mathf.Max(0, _cfgDefaultKillPoints.Value);
            if (_cfgTopCount != null) rules.TopCount = Mathf.Clamp(_cfgTopCount.Value, 1, 50);
            if (_cfgAllowRepeatedBossPoints != null) rules.AllowRepeatedBossPoints = _cfgAllowRepeatedBossPoints.Value;
            if (_cfgDebugLogging != null) rules.DebugLogging = _cfgDebugLogging.Value;
            if (_cfgLogHitReports != null) rules.LogHitReports = _cfgLogHitReports.Value;
            if (_cfgLogKillReports != null) rules.LogKillReports = _cfgLogKillReports.Value;
            if (_cfgLogSkillReports != null) rules.LogSkillReports = _cfgLogSkillReports.Value;
            if (_cfgLogPendingKillReports != null) rules.LogPendingKillReports = _cfgLogPendingKillReports.Value;
            if (_cfgLogSnapshotRequests != null) rules.LogSnapshotRequests = _cfgLogSnapshotRequests.Value;
            if (_cfgLogPointsChanges != null) rules.LogPointsChanges = _cfgLogPointsChanges.Value;
            if (_cfgEnableSkillPoints != null) rules.EnableSkillPoints = _cfgEnableSkillPoints.Value;
            if (_cfgIgnoreTamedKills != null) rules.IgnoreTamedKills = _cfgIgnoreTamedKills.Value;
            if (_cfgEnableMarketplaceQuestPoints != null) rules.EnableMarketplaceQuestPoints = _cfgEnableMarketplaceQuestPoints.Value;
            if (_cfgEnableFishingPoints != null) rules.EnableFishingPoints = _cfgEnableFishingPoints.Value;
            if (_cfgEnableDeathPenalty != null) rules.EnableDeathPenalty = _cfgEnableDeathPenalty.Value;
            if (_cfgDeathPenaltyUseMultiplier != null) rules.DeathPenaltyUseMultiplier = _cfgDeathPenaltyUseMultiplier.Value;
            if (_cfgDeathPenaltyPerDeath != null) rules.DeathPenaltyPerDeath = Mathf.Max(0, _cfgDeathPenaltyPerDeath.Value);
            if (_cfgEnableExplorationJackpots != null) rules.EnableExplorationJackpots = _cfgEnableExplorationJackpots.Value;
            if (_cfgRewardClaimsEnabled != null) rules.RewardClaimsEnabled = _cfgRewardClaimsEnabled.Value;
            if (_cfgRewardClaimCycleId != null) rules.RewardClaimCycleId = SafeLimit(_cfgRewardClaimCycleId.Value, 64);
            if (_cfgRewardTop1MinPoints != null) rules.RewardTop1MinPoints = Mathf.Max(0, _cfgRewardTop1MinPoints.Value);
            if (_cfgRewardTop1Label != null) rules.RewardTop1Label = SafeLimit(_cfgRewardTop1Label.Value, 96);
            if (_cfgRewardTop1Prefab != null) rules.RewardTop1Prefab = SafeLimit(_cfgRewardTop1Prefab.Value, 96);
            if (_cfgRewardTop1Amount != null) rules.RewardTop1Amount = Mathf.Max(0, _cfgRewardTop1Amount.Value);
            if (_cfgRewardTop2MinPoints != null) rules.RewardTop2MinPoints = Mathf.Max(0, _cfgRewardTop2MinPoints.Value);
            if (_cfgRewardTop2Label != null) rules.RewardTop2Label = SafeLimit(_cfgRewardTop2Label.Value, 96);
            if (_cfgRewardTop2Prefab != null) rules.RewardTop2Prefab = SafeLimit(_cfgRewardTop2Prefab.Value, 96);
            if (_cfgRewardTop2Amount != null) rules.RewardTop2Amount = Mathf.Max(0, _cfgRewardTop2Amount.Value);
            if (_cfgRewardTop3MinPoints != null) rules.RewardTop3MinPoints = Mathf.Max(0, _cfgRewardTop3MinPoints.Value);
            if (_cfgRewardTop3Label != null) rules.RewardTop3Label = SafeLimit(_cfgRewardTop3Label.Value, 96);
            if (_cfgRewardTop3Prefab != null) rules.RewardTop3Prefab = SafeLimit(_cfgRewardTop3Prefab.Value, 96);
            if (_cfgRewardTop3Amount != null) rules.RewardTop3Amount = Mathf.Max(0, _cfgRewardTop3Amount.Value);
            if (_cfgPointsExchangeEnabled != null) rules.PointsExchangeEnabled = _cfgPointsExchangeEnabled.Value;
            if (_cfgPointsExchangePrefab != null) rules.PointsExchangePrefab = SafeLimit(_cfgPointsExchangePrefab.Value, 96);
            if (_cfgPointsExchangeCoinsPerPoint != null) rules.PointsExchangeCoinsPerPoint = Mathf.Max(1, _cfgPointsExchangeCoinsPerPoint.Value);
            if (_cfgPointsExchangeMinPoints != null) rules.PointsExchangeMinPoints = Mathf.Max(0, _cfgPointsExchangeMinPoints.Value);
            if (_cfgPointsExchangeMaxPointsPerRequest != null) rules.PointsExchangeMaxPointsPerRequest = Mathf.Max(0, _cfgPointsExchangeMaxPointsPerRequest.Value);

            string combatCategoryRaw = BuildCombatCategoryRulesFromBiomeEntries();
            string productionCategoryRaw = _cfgProductionCategoryRules != null ? _cfgProductionCategoryRules.Value : DefaultProductionCategoryRules;

            Dictionary<string, string> combatCategories;
            foreach (KeyValuePair<string, int> pair in ParseHudCategorizedPointRules(combatCategoryRaw, out combatCategories))
            {
                if (IsFishPrefab(pair.Key))
                    continue;

                rules.KillPoints[pair.Key] = Mathf.Max(0, pair.Value);
            }

            Dictionary<string, string> productionCategories;
            foreach (KeyValuePair<string, int> pair in ParseHudCategorizedPointRules(productionCategoryRaw, out productionCategories))
                rules.CraftPoints[pair.Key] = Mathf.Max(0, pair.Value);

            rules.CombatHudCategories = combatCategories;
            rules.ProductionHudCategories = productionCategories;

            foreach (KeyValuePair<string, ConfigEntry<int>> pair in _cfgKillPointEntries)
            {
                if (pair.Value == null)
                    continue;

                string killKey = SafeKey(pair.Key);
                if (string.IsNullOrWhiteSpace(killKey) || IsFishPrefab(killKey))
                    continue;

                rules.KillPoints[killKey] = Mathf.Max(0, pair.Value.Value);
            }

            foreach (KeyValuePair<string, ConfigEntry<int>> pair in _cfgBossPointEntries)
                rules.BossPoints[pair.Key] = pair.Value != null ? pair.Value.Value : 0;

            foreach (KeyValuePair<string, ConfigEntry<int>> pair in _cfgFishingPointEntries)
            {
                string fishKey = NormalizeFishPrefabName(pair.Key);
                if (!IsFishPrefab(fishKey))
                    continue;

                rules.FishingPoints[fishKey] = pair.Value != null ? pair.Value.Value : 0;
            }

            foreach (KeyValuePair<string, ConfigEntry<string>> pair in _cfgSkillMilestoneJackpotPointEntries)
                rules.SkillMilestoneJackpotPoints[pair.Key] = pair.Value != null ? ParseSkillMilestoneMap(pair.Value.Value) : new Dictionary<int, int>();

            if (_cfgFarmJackpotRules != null)
            {
                foreach (KeyValuePair<string, JackpotRule> pair in ParseDelimitedJackpotRules(_cfgFarmJackpotRules.Value))
                    rules.FarmJackpots[pair.Key] = pair.Value;
            }

            if (_cfgUniqueCraftJackpotRules != null)
            {
                foreach (KeyValuePair<string, JackpotRule> pair in ParseDelimitedJackpotRules(_cfgUniqueCraftJackpotRules.Value))
                    rules.UniqueCraftJackpots[pair.Key] = pair.Value;
            }

            if (_cfgMarketplaceQuestPointMap != null)
            {
                foreach (KeyValuePair<string, int> pair in ParseMarketplaceQuestPointMap(_cfgMarketplaceQuestPointMap.Value))
                    rules.MarketplaceQuestPoints[pair.Key] = pair.Value;
            }

            if (_cfgDeathPenaltyRules != null)
                rules.DeathPenaltyRules = ParseDeathPenaltyRules(_cfgDeathPenaltyRules.Value);

            if (_cfgExplorationMapJackpotRules != null)
                rules.ExplorationMapJackpots = ParseDeathPenaltyRules(_cfgExplorationMapJackpotRules.Value);

            _rules = rules;
            RebuildMarketplaceQuestPointCache();
        }

        private void RebuildMarketplaceQuestPointCache()
        {
            _marketplaceQuestPointsByUid.Clear();

            if (_rules == null || _rules.MarketplaceQuestPoints == null)
                return;

            foreach (KeyValuePair<string, int> pair in _rules.MarketplaceQuestPoints)
            {
                string questKey = SafeMarketplaceQuestKey(pair.Key);
                int points = pair.Value;

                if (string.IsNullOrWhiteSpace(questKey) || points == 0)
                    continue;

                int uid = MarketplaceQuestKeyToUid(questKey);
                if (uid == 0)
                    continue;

                _marketplaceQuestPointsByUid[uid] = new MarketplaceQuestPointRule
                {
                    QuestKey = questKey,
                    Points = points
                };
            }
        }

        private string SafeMarketplaceQuestKey(string questKey)
        {
            questKey = SafeLimit(questKey, 128).Trim().ToLowerInvariant();
            questKey = questKey.Replace("|", "/");
            return questKey;
        }

        private int MarketplaceQuestKeyToUid(string questKey)
        {
            questKey = SafeMarketplaceQuestKey(questKey);
            if (string.IsNullOrWhiteSpace(questKey))
                return 0;

            try
            {
                return StringExtensionMethods.GetStableHashCode(questKey);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Ranking] Falha ao converter quest key do Marketplace em UID: " + questKey + " erro=" + ex.Message);
                return 0;
            }
        }

        private bool TryGetMarketplaceQuestRule(int questUid, out MarketplaceQuestPointRule rule)
        {
            rule = null;

            if (_rules == null || !_rules.EnableMarketplaceQuestPoints)
                return false;

            return _marketplaceQuestPointsByUid.TryGetValue(questUid, out rule) && rule != null && rule.Points != 0;
        }
    }
}
