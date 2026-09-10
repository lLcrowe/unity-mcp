using NUnit.Framework;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using UnityEditor;

namespace MCPForUnityTests.Editor.Services
{
    /// <summary>
    /// Unit tests for EditorConfigurationCache.
    /// </summary>
    [TestFixture]
    public class EditorConfigurationCacheTests
    {
        private bool _originalUseHttpTransport;
        private bool _originalDebugLogs;
        private string _originalUvxPath;
        private bool _hadProjectHttpBaseUrl;
        private string _originalProjectHttpBaseUrl;
        private bool _hadLegacyHttpBaseUrl;
        private string _originalLegacyHttpBaseUrl;

        [SetUp]
        public void SetUp()
        {
            // Save original values
            _originalUseHttpTransport = EditorPrefs.GetBool(EditorPrefKeys.UseHttpTransport, true);
            _originalDebugLogs = EditorPrefs.GetBool(EditorPrefKeys.DebugLogs, false);
            _originalUvxPath = EditorPrefs.GetString(EditorPrefKeys.UvxPathOverride, string.Empty);
            _hadProjectHttpBaseUrl = EditorPrefs.HasKey(EditorPrefKeys.HttpBaseUrl);
            _originalProjectHttpBaseUrl = EditorPrefs.GetString(EditorPrefKeys.HttpBaseUrl, string.Empty);
            _hadLegacyHttpBaseUrl = EditorPrefs.HasKey(EditorPrefKeys.LegacyHttpBaseUrl);
            _originalLegacyHttpBaseUrl = EditorPrefs.GetString(EditorPrefKeys.LegacyHttpBaseUrl, string.Empty);

            // Refresh cache to ensure clean state
            EditorConfigurationCache.Instance.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original values
            EditorConfigurationCache.Instance.UnpinStdioForSession();
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, _originalUseHttpTransport);
            EditorPrefs.SetBool(EditorPrefKeys.DebugLogs, _originalDebugLogs);
            EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, _originalUvxPath);
            RestoreStringPref(EditorPrefKeys.HttpBaseUrl, _hadProjectHttpBaseUrl, _originalProjectHttpBaseUrl);
            RestoreStringPref(EditorPrefKeys.LegacyHttpBaseUrl, _hadLegacyHttpBaseUrl, _originalLegacyHttpBaseUrl);

            // Refresh cache
            EditorConfigurationCache.Instance.Refresh();
        }

        private static void RestoreStringPref(string key, bool existed, string value)
        {
            if (existed)
            {
                EditorPrefs.SetString(key, value);
            }
            else
            {
                EditorPrefs.DeleteKey(key);
            }
        }

        #region Singleton Tests

        [Test]
        public void Instance_ReturnsSameInstance()
        {
            // Act
            var instance1 = EditorConfigurationCache.Instance;
            var instance2 = EditorConfigurationCache.Instance;

            // Assert
            Assert.AreSame(instance1, instance2, "Should return the same singleton instance");
        }

        [Test]
        public void Instance_IsNotNull()
        {
            // Assert
            Assert.IsNotNull(EditorConfigurationCache.Instance);
        }

        #endregion

        #region Read Tests

        [Test]
        public void UseHttpTransport_ReturnsEditorPrefsValue()
        {
            // Arrange
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, true);
            EditorConfigurationCache.Instance.Refresh();

            // Assert
            Assert.IsTrue(EditorConfigurationCache.Instance.UseHttpTransport);

            // Arrange - change value
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, false);
            EditorConfigurationCache.Instance.Refresh();

            // Assert
            Assert.IsFalse(EditorConfigurationCache.Instance.UseHttpTransport);
        }

        [Test]
        public void DebugLogs_ReturnsEditorPrefsValue()
        {
            // Arrange
            EditorPrefs.SetBool(EditorPrefKeys.DebugLogs, true);
            EditorConfigurationCache.Instance.Refresh();

            // Assert
            Assert.IsTrue(EditorConfigurationCache.Instance.DebugLogs);
        }

        [Test]
        public void UvxPathOverride_ReturnsEditorPrefsValue()
        {
            // Arrange
            string testPath = "/custom/path/to/uvx";
            EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, testPath);
            EditorConfigurationCache.Instance.Refresh();

            // Assert
            Assert.AreEqual(testPath, EditorConfigurationCache.Instance.UvxPathOverride);
        }

        [Test]
        public void HttpBaseUrl_UsesCurrentProjectKeyAndIgnoresLegacyGlobalValue()
        {
            string projectUrl = "http://127.0.0.1:58123";
            string legacyUrl = "http://127.0.0.1:58124";
            EditorPrefs.SetString(EditorPrefKeys.LegacyHttpBaseUrl, legacyUrl);
            EditorPrefs.SetString(EditorPrefKeys.HttpBaseUrl, projectUrl);

            EditorConfigurationCache.Instance.Refresh();

            Assert.AreEqual(projectUrl, EditorConfigurationCache.Instance.HttpBaseUrl);
            Assert.AreNotEqual(EditorPrefKeys.LegacyHttpBaseUrl, EditorPrefKeys.HttpBaseUrl);
            Assert.That(
                EditorPrefKeys.HttpBaseUrl,
                Does.EndWith("_" + ProjectIdentityUtility.GetProjectHash()));
        }

        [Test]
        public void HttpBaseUrl_UserCanReplaceThePortChosenForCurrentProject()
        {
            string firstChoice = "http://127.0.0.1:58125";
            string secondChoice = "http://127.0.0.1:58126";

            HttpEndpointUtility.SaveLocalBaseUrl(firstChoice);
            Assert.AreEqual(firstChoice, HttpEndpointUtility.GetLocalBaseUrl());

            HttpEndpointUtility.SaveLocalBaseUrl(secondChoice);
            EditorConfigurationCache.Instance.Refresh();

            Assert.AreEqual(secondChoice, HttpEndpointUtility.GetLocalBaseUrl());
            Assert.AreEqual(secondChoice, EditorConfigurationCache.Instance.HttpBaseUrl);
        }

        [Test]
        public void HttpBaseUrl_SavingCurrentProjectDoesNotOverwriteAnotherProject()
        {
            string otherProjectKey = EditorPrefKeys.LegacyHttpBaseUrl + "_another-project";
            string otherProjectUrl = "http://127.0.0.1:58128";
            bool hadOtherProjectUrl = EditorPrefs.HasKey(otherProjectKey);
            string originalOtherProjectUrl = EditorPrefs.GetString(otherProjectKey, string.Empty);

            try
            {
                EditorPrefs.SetString(otherProjectKey, otherProjectUrl);

                HttpEndpointUtility.SaveLocalBaseUrl("http://127.0.0.1:58129");

                Assert.AreEqual(
                    otherProjectUrl,
                    EditorPrefs.GetString(otherProjectKey, string.Empty),
                    "Saving this project's port must not mutate another project's URL.");
            }
            finally
            {
                RestoreStringPref(otherProjectKey, hadOtherProjectUrl, originalOtherProjectUrl);
            }
        }

        #endregion

        #region Write Tests

        [Test]
        public void SetUseHttpTransport_UpdatesCacheAndEditorPrefs()
        {
            // Arrange
            bool initialValue = EditorConfigurationCache.Instance.UseHttpTransport;
            bool newValue = !initialValue;

            // Act
            EditorConfigurationCache.Instance.SetUseHttpTransport(newValue);

            // Assert - cache is updated
            Assert.AreEqual(newValue, EditorConfigurationCache.Instance.UseHttpTransport);

            // Assert - EditorPrefs is updated
            Assert.AreEqual(newValue, EditorPrefs.GetBool(EditorPrefKeys.UseHttpTransport, !newValue));
        }

        [Test]
        public void SetDebugLogs_UpdatesCacheAndEditorPrefs()
        {
            // Act
            EditorConfigurationCache.Instance.SetDebugLogs(true);

            // Assert
            Assert.IsTrue(EditorConfigurationCache.Instance.DebugLogs);
            Assert.IsTrue(EditorPrefs.GetBool(EditorPrefKeys.DebugLogs, false));
        }

        [Test]
        public void SetUvxPathOverride_UpdatesCacheAndEditorPrefs()
        {
            // Arrange
            string testPath = "/test/uvx/path";

            // Act
            EditorConfigurationCache.Instance.SetUvxPathOverride(testPath);

            // Assert
            Assert.AreEqual(testPath, EditorConfigurationCache.Instance.UvxPathOverride);
            Assert.AreEqual(testPath, EditorPrefs.GetString(EditorPrefKeys.UvxPathOverride, string.Empty));
        }

        [Test]
        public void SetUvxPathOverride_NullBecomesEmptyString()
        {
            // Act
            EditorConfigurationCache.Instance.SetUvxPathOverride(null);

            // Assert
            Assert.AreEqual(string.Empty, EditorConfigurationCache.Instance.UvxPathOverride);
        }

        #endregion

        #region Change Notification Tests

        [Test]
        public void SetUseHttpTransport_FiresOnConfigurationChanged()
        {
            // Arrange
            string changedKey = null;
            EditorConfigurationCache.Instance.OnConfigurationChanged += (key) => changedKey = key;
            bool initialValue = EditorConfigurationCache.Instance.UseHttpTransport;

            // Act
            EditorConfigurationCache.Instance.SetUseHttpTransport(!initialValue);

            // Assert
            Assert.AreEqual(nameof(EditorConfigurationCache.UseHttpTransport), changedKey);

            // Cleanup
            EditorConfigurationCache.Instance.OnConfigurationChanged -= (key) => changedKey = key;
        }

        [Test]
        public void SetSameValue_DoesNotFireOnConfigurationChanged()
        {
            // Arrange
            int eventCount = 0;
            EditorConfigurationCache.Instance.OnConfigurationChanged += (key) => eventCount++;
            bool currentValue = EditorConfigurationCache.Instance.UseHttpTransport;

            // Act - set same value
            EditorConfigurationCache.Instance.SetUseHttpTransport(currentValue);

            // Assert - no event fired
            Assert.AreEqual(0, eventCount, "Should not fire event when value doesn't change");

            // Cleanup
            EditorConfigurationCache.Instance.OnConfigurationChanged -= (key) => eventCount++;
        }

        #endregion

        #region InvalidateKey Tests

        [Test]
        public void InvalidateKey_RefreshesSingleValue()
        {
            // Arrange
            EditorConfigurationCache.Instance.SetDebugLogs(false);
            Assert.IsFalse(EditorConfigurationCache.Instance.DebugLogs);

            // Directly modify EditorPrefs (simulating external change)
            EditorPrefs.SetBool(EditorPrefKeys.DebugLogs, true);

            // Act
            EditorConfigurationCache.Instance.InvalidateKey(nameof(EditorConfigurationCache.DebugLogs));

            // Assert
            Assert.IsTrue(EditorConfigurationCache.Instance.DebugLogs);
        }

        [Test]
        public void InvalidateKey_FiresOnConfigurationChanged()
        {
            // Arrange
            string changedKey = null;
            EditorConfigurationCache.Instance.OnConfigurationChanged += (key) => changedKey = key;

            // Act
            EditorConfigurationCache.Instance.InvalidateKey(nameof(EditorConfigurationCache.DebugLogs));

            // Assert
            Assert.AreEqual(nameof(EditorConfigurationCache.DebugLogs), changedKey);

            // Cleanup
            EditorConfigurationCache.Instance.OnConfigurationChanged -= (key) => changedKey = key;
        }

        #endregion

        #region Refresh Tests

        [Test]
        public void Refresh_UpdatesAllCachedValues()
        {
            // Arrange - directly set EditorPrefs
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, false);
            EditorPrefs.SetBool(EditorPrefKeys.DebugLogs, true);
            EditorPrefs.SetString(EditorPrefKeys.UvxPathOverride, "/refreshed/path");

            // Act
            EditorConfigurationCache.Instance.Refresh();

            // Assert
            Assert.IsFalse(EditorConfigurationCache.Instance.UseHttpTransport);
            Assert.IsTrue(EditorConfigurationCache.Instance.DebugLogs);
            Assert.AreEqual("/refreshed/path", EditorConfigurationCache.Instance.UvxPathOverride);
        }

        #endregion

        #region Session Pin Tests

        [Test]
        public void PinStdioForSession_OverridesHttpPreference_WithoutWritingEditorPrefs()
        {
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, true);
            EditorConfigurationCache.Instance.Refresh();
            Assert.IsTrue(EditorConfigurationCache.Instance.UseHttpTransport);

            EditorConfigurationCache.Instance.PinStdioForSession();

            Assert.IsFalse(EditorConfigurationCache.Instance.UseHttpTransport);
            Assert.IsTrue(EditorPrefs.GetBool(EditorPrefKeys.UseHttpTransport, false),
                "The pin must not rewrite the developer's persisted transport preference");
        }

        [Test]
        public void PinStdioForSession_SurvivesRefresh()
        {
            // Refresh() is what a fresh cache instance runs after a domain reload; the pin lives
            // in SessionState precisely so it survives that.
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, true);
            EditorConfigurationCache.Instance.PinStdioForSession();

            EditorConfigurationCache.Instance.Refresh();

            Assert.IsTrue(SessionState.GetBool(EditorConfigurationCache.SessionKeyForceStdio, false));
            Assert.IsFalse(EditorConfigurationCache.Instance.UseHttpTransport);
        }

        [Test]
        public void UnpinStdioForSession_RestoresPreferenceAndNotifies()
        {
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, true);
            EditorConfigurationCache.Instance.Refresh();
            EditorConfigurationCache.Instance.PinStdioForSession();

            string changedKey = null;
            void Handler(string key) => changedKey = key;
            EditorConfigurationCache.Instance.OnConfigurationChanged += Handler;
            try
            {
                EditorConfigurationCache.Instance.UnpinStdioForSession();
            }
            finally
            {
                EditorConfigurationCache.Instance.OnConfigurationChanged -= Handler;
            }

            Assert.AreEqual(nameof(EditorConfigurationCache.UseHttpTransport), changedKey);
            Assert.IsTrue(EditorConfigurationCache.Instance.UseHttpTransport);
            Assert.IsFalse(SessionState.GetBool(EditorConfigurationCache.SessionKeyForceStdio, false));
        }

        #endregion
    }
}
