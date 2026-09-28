using BreathOfEclipse.Core;
using NUnit.Framework;

namespace BreathOfEclipse.Tests
{
    public class SaveSystemTests
    {
        private ISaveStorage _previous;

        [SetUp]
        public void UseMemoryStorage()
        {
            _previous = SaveSystem.Storage;
            SaveSystem.Storage = new MemorySaveStorage();
            SaveSystem.OverrideSettings(null);
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.Storage = _previous;
            SaveSystem.OverrideSettings(null);
        }

        [Test]
        public void Settings_RoundTrip()
        {
            var s = SaveSystem.LoadSettings();
            s.masterVolume = 0.3f;
            s.mouseSensitivity = 2.5f;
            s.invertY = true;
            s.lastEquippedStyle = "thunder";
            s.skipUltimateCinematics = true;
            s.bindingOverrides = "{\"bindings\":[]}";
            SaveSystem.SaveSettings();

            SaveSystem.OverrideSettings(null);
            var loaded = SaveSystem.LoadSettings();
            Assert.AreEqual(0.3f, loaded.masterVolume, 0.0001f);
            Assert.AreEqual(2.5f, loaded.mouseSensitivity, 0.0001f);
            Assert.IsTrue(loaded.invertY);
            Assert.AreEqual("thunder", loaded.lastEquippedStyle);
            Assert.IsTrue(loaded.skipUltimateCinematics);
            Assert.AreEqual("{\"bindings\":[]}", loaded.bindingOverrides);
        }

        [Test]
        public void CorruptedFile_FallsBackToDefaults()
        {
            SaveSystem.Storage.Write(SaveSystem.SettingsKey, "{ this is not json");
            var loaded = SaveSystem.LoadSettings();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(new GameSettings().masterVolume, loaded.masterVolume, 0.0001f);
        }

        [Test]
        public void OutOfRangeValues_AreSanitized()
        {
            SaveSystem.Storage.Write(SaveSystem.SettingsKey, "{\"masterVolume\": 7, \"fieldOfView\": 400, \"lastEquippedStyle\": \"\"}");
            var loaded = SaveSystem.LoadSettings();
            Assert.AreEqual(1f, loaded.masterVolume, 0.0001f);
            Assert.AreEqual(90f, loaded.fieldOfView, 0.0001f);
            Assert.AreEqual("tidal", loaded.lastEquippedStyle);
        }

        [Test]
        public void ResetToDefaults_RaisesSettingsChanged()
        {
            int raised = 0;
            void Handler(GameSettings _) => raised++;
            SaveSystem.SettingsChanged += Handler;
            try
            {
                SaveSystem.ResetToDefaults();
            }
            finally
            {
                SaveSystem.SettingsChanged -= Handler;
            }
            Assert.AreEqual(1, raised);
        }
    }
}
