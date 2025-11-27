using NUnit.Framework;
using Strada.Core.Bridge;
using Strada.Core.MVCS;

namespace Strada.Core.Tests.Runtime.MVCS
{
    [TestFixture]
    public class ModelReactiveTests
    {
        [Test]
        public void Model_Initialize_SetsIsInitialized()
        {
            var model = new TestModel();

            Assert.IsFalse(model.IsInit);

            model.Initialize();

            Assert.IsTrue(model.IsInit);
        }

        [Test]
        public void Model_CreateProperty_ReturnsReactiveProperty()
        {
            var model = new PropertyModel();
            model.Initialize();

            Assert.IsNotNull(model.GetHealthProperty());
        }

        [Test]
        public void Model_Property_NotifiesOnChange()
        {
            var model = new PropertyModel();
            model.Initialize();

            int notifyCount = 0;
            model.GetHealthProperty().Subscribe(_ => notifyCount++);

            model.SetHealth(100);
            model.SetHealth(50);

            Assert.AreEqual(2, notifyCount);
        }

        [Test]
        public void Model_Property_DoesNotNotifyOnSameValue()
        {
            var model = new PropertyModel();
            model.Initialize();

            int notifyCount = 0;
            model.GetHealthProperty().Subscribe(_ => notifyCount++);

            model.SetHealth(100);
            model.SetHealth(100);

            Assert.AreEqual(1, notifyCount);
        }

        [Test]
        public void Model_CreateCollection_ReturnsReactiveCollection()
        {
            var model = new CollectionModel();
            model.Initialize();

            Assert.IsNotNull(model.GetItems());
        }

        [Test]
        public void Model_Collection_NotifiesOnAdd()
        {
            var model = new CollectionModel();
            model.Initialize();

            int addCount = 0;
            model.GetItems().OnAdd(_ => addCount++);

            model.AddItem("A");
            model.AddItem("B");

            Assert.AreEqual(2, addCount);
        }

        [Test]
        public void Model_Dispose_ClearsProperties()
        {
            var model = new PropertyModel();
            model.Initialize();

            int notifyCount = 0;
            model.GetHealthProperty().Subscribe(_ => notifyCount++);

            model.Dispose();
            model.Initialize();

            Assert.AreEqual(0, notifyCount);
        }

        [Test]
        public void DataModel_Initialize_CreatesData()
        {
            var model = new DataModel();
            model.Initialize();

            Assert.IsNotNull(model.GetData());
        }

        [Test]
        public void DataModel_UpdateData_NotifiesChange()
        {
            var model = new DataModel();
            model.Initialize();

            int notifyCount = 0;
            model.SubscribeToData(_ => notifyCount++);

            model.UpdatePlayerData(d => d.Score = 100);

            Assert.AreEqual(1, notifyCount);
        }

        [Test]
        public void ReactiveModel_Property_CreatesByName()
        {
            var model = new NamedPropertyModel();
            model.Initialize();

            var prop1 = model.GetNamedProperty("health");
            var prop2 = model.GetNamedProperty("health");

            Assert.AreSame(prop1, prop2);
        }

        private class TestModel : Model
        {
            public bool IsInit => IsInitialized;
        }

        private class PropertyModel : Model
        {
            private ReactiveProperty<int> _health;

            protected override void OnInitialize()
            {
                _health = CreateProperty(0);
            }

            public ReactiveProperty<int> GetHealthProperty() => _health;
            public void SetHealth(int value) => _health.Value = value;
        }

        private class CollectionModel : Model
        {
            private ReactiveCollection<string> _items;

            protected override void OnInitialize()
            {
                _items = CreateCollection<string>();
            }

            public ReactiveCollection<string> GetItems() => _items;
            public void AddItem(string item) => _items.Add(item);
        }

        private class PlayerData
        {
            public int Score;
            public string Name;
        }

        private class DataModel : Model<PlayerData>
        {
            public PlayerData GetData() => Data;

            public void UpdatePlayerData(System.Action<PlayerData> updater)
            {
                UpdateData(updater);
            }

            public void SubscribeToData(System.Action<PlayerData> handler)
            {
                DataProperty.Subscribe(handler);
            }
        }

        private class NamedPropertyModel : ReactiveModel
        {
            public IReadOnlyReactiveProperty<int> GetNamedProperty(string name)
            {
                return Property<int>(name, 0);
            }
        }
    }
}
