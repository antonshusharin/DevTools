using System;
using System.Collections.Generic;

namespace Accessibility
{
	class AccessibleBattlegroundsLeaderboardMgr
	{
		private static AccessibleBattlegroundsLeaderboardMgr s_instance;

		private bool m_readingLeaderboard;

		private AccessibleListOfItems<AccessiblePlayerLeaderboardTeam> m_accessibleTeams;

		internal static AccessibleBattlegroundsLeaderboardMgr Get()
		{
			if (s_instance == null)
			{
				s_instance = new AccessibleBattlegroundsLeaderboardMgr();
			}

			return s_instance;
		}

		public void ReadNextOpponent()
		{
			var nextOpponentTeam = PlayerLeaderboardManager.Get().GetNextOpponentTile().m_parent;
			StartReadingLeaderboard(GetTeamIndex(nextOpponentTeam));
		}

		public void ReadMyself()
		{
			var myTeam = PlayerLeaderboardManager.Get().GetMyTile().m_parent;
			StartReadingLeaderboard(GetTeamIndex(myTeam));
		}

		public void ReadNextOpponentToEnd()
		{
			ReadTeam(PlayerLeaderboardManager.Get().GetNextOpponentTile().m_parent);
		}

		public void ReadMyselfToEnd()
		{
			ReadTeam(PlayerLeaderboardManager.Get().GetMyTile().m_parent);
		}

		private static void ReadTeam(PlayerLeaderboardTeam team)
		{
			if (team == null)
			{
				return;
			}

			var accessibleTeam = CreateAccessibleTeam(team);
			accessibleTeam.ReadAllLines();
		}

		internal int GetNumTeamsAlive()
		{
			var teams = PlayerLeaderboardManager.Get().m_teams;

			int ret = 0;

			foreach (var team in teams)
			{
				if (team?.Members[0].Entity.GetRealTimeRemainingHP() > 0)
				{
					ret++;
				}
			}

			return ret;
		}

		internal bool HandleAccessibleInput()
		{
			if (AccessibleKey.BATTLEGROUNDS_READ_LEADERBOARD.IsPressed() && !m_readingLeaderboard)
			{
				StartReadingLeaderboard();
				return true;
			}
			else if (AccessibleKey.BACK.IsPressed() && m_readingLeaderboard)
			{
				StopReadingLeaderboard();
				AccessibleInputMgr.HideMouse();
				return true;
			}
			else if (m_readingLeaderboard)
			{
				AccessibleInputMgr.MoveMouseTo(m_accessibleTeams.GetItemBeingRead().GetCard());

				return m_accessibleTeams.HandleAccessibleInput();
			}

			return false;
		}

		internal void StartReadingLeaderboard(int fromIndex = -1)
		{
			var teams = PlayerLeaderboardManager.Get().m_teams;

			if (teams == null || teams.Count == 0)
			{
				return;
			}

			if (fromIndex < 0)
			{
				fromIndex = GetMyTeamIndex();
			}
			else if (fromIndex >= teams.Count)
			{
				fromIndex = teams.Count - 1;
			}

			var accessibleTeams = new List<AccessiblePlayerLeaderboardTeam>();

			foreach (var team in teams)
			{
				accessibleTeams.Add(CreateAccessibleTeam(team));
			}

			m_accessibleTeams = new AccessibleListOfItems<AccessiblePlayerLeaderboardTeam>(AccessibleGameplay.Get(), accessibleTeams);
			m_readingLeaderboard = true;

			m_accessibleTeams.StartReadingFromIndex(fromIndex);
		}

		private int GetMyTeamIndex()
		{
			var myTile = PlayerLeaderboardManager.Get().GetMyTile();

			if (myTile == null)
			{
				return 0;
			}

			return GetTeamIndex(myTile.m_parent);
		}

		internal void StopReadingLeaderboard()
		{
			m_readingLeaderboard = false;
		}

		internal bool IsReadingLeaderboard()
		{
			return m_readingLeaderboard;
		}

		private int GetTeamIndex(PlayerLeaderboardTeam team)
		{
			var teams = PlayerLeaderboardManager.Get().m_teams;

			for (int i = 0; i < teams.Count; i++)
			{
				var t = teams[i];

				if (t == team)
				{
					return i;
				}
			}

			return 0; // Allow StartReading to fail gracefully
		}

		private static AccessiblePlayerLeaderboardTeam CreateAccessibleTeam(PlayerLeaderboardTeam team)
		{
			switch (team)
			{
				case PlayerLeaderboardSoloTeam solo:
					return new AccessiblePlayerLeaderboardSoloTeam(AccessibleGameplay.Get(), solo);
				case PlayerLeaderboardDuosTeam duos:
					return new AccessiblePlayerLeaderboardDuosTeam(AccessibleGameplay.Get(), duos);
			}
			return null;
		}
	}
}
