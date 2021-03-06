using KeePass.Plugins;
using KeePassLib;
using System;
using KeePassLib.Security;

namespace KeePassProtonfile
{
    internal class Configuration
    {
        private IPluginHost m_host;
		private PwGroup protonGroup;
		private bool updated;
        public Configuration(IPluginHost mHost)
        {
			m_host = mHost;
			updated = false;
		}
		// call this method when db is opened
        public void init() {
			PwDatabase pd = m_host.Database;
			if ((pd == null) || !pd.IsOpen) { return; }

			PwGroup pgParent = pd.RootGroup;
			var groups = pgParent.GetGroups(false);
			bool found = false;
			foreach(var e in groups)
            {
				if (e.Name == "Protonfile") { 
					found = true;
					protonGroup = e;
				};
            }
			if (!found)
            {
				PwGroup pg = new PwGroup(true, true, "Protonfile", PwIcon.Home);
				pgParent.AddGroup(pg, true);
				protonGroup = pg;
			};

			if (getEntry("auth") == null) setEntry("auth", "");
			if (getEntry("multipleBackups") == null) setEntry("multipleBackups", "false");
			if (getEntry("multipleBackupsNum") == null) setEntry("multipleBackupsNum", "1");
			if (getEntry("filename") == null) setEntry("filename", "database");
			if (getEntry("destinationFolder") == null) setEntry("destinationFolder", "keepass");
			if (getEntry("operatingMode") == null) setEntry("operatingMode", "backup");

			m_host.MainWindow.UpdateUI(false, null, true, null, false, null, this.updated);
		}
		public PwEntry getEntry(String title)
        {
			var entries = protonGroup.GetEntries(false);
			PwEntry found = null;

			foreach(var e in entries) {
				var eTitle = e.Strings.Get(PwDefs.TitleField);
				if (eTitle.ReadString() == title) found = e;
				if (eTitle.ReadString() == title) break;
			}
			return found;
		}
		public PwEntry setEntry(String title, String value, String password = "")
        {
			this.updated = true;
			var entries = protonGroup.GetEntries(false);
			PwEntry entry = null;

			foreach (var e in entries)
			{
				var eTitle = e.Strings.Get(PwDefs.TitleField);
				if (eTitle.ReadString() == title) entry = e;
				if (eTitle.ReadString() == title) break;
			}

			if (entry == null)
			{
				entry = new PwEntry(true, true);
				protonGroup.AddEntry(entry, true);
			}

			PwDatabase pd = m_host.Database;

			entry.Strings.Set(PwDefs.TitleField, new ProtectedString(
				pd.MemoryProtection.ProtectTitle, title));
			entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(
				pd.MemoryProtection.ProtectUserName, value));
			entry.Strings.Set(PwDefs.PasswordField, new ProtectedString(
				pd.MemoryProtection.ProtectPassword, password));

			entry.Touch(true);
			m_host.MainWindow.UpdateUI(false, null, true, null, true, null, true);

			return entry;
		}
    }
}