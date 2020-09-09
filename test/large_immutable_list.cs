using Lokad.LargeImmutable.Mapping;
using MessagePack;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Lokad.LargeImmutable.Tests
{
    public sealed class large_immutable_list
    {
        [InlineData(false)]
        [InlineData(true)]
        [Theory]
        public void empty(bool save)
        {
            var empty = Empty();

            if (save) empty = SaveAndReload(empty);

            Assert.True(empty.IsEmpty);
            Assert.Empty(empty);

            foreach (var _ in empty)
                Assert.True(false);
        }

        [InlineData(false)]
        [InlineData(true)]
        [Theory]
        public void one_element(bool save)
        {
            var list = Empty().Add(Bob);

            if (save) list = SaveAndReload(list);

            Assert.False(list.IsEmpty);
            Assert.Collection(list, 
                a1 => Assert.Equal(Bob, a1));

            Assert.Equal(Bob, list[0]);
        }

        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [Theory]
        public void two_elements(int save)
        {
            var list = Empty().Add(Bob);

            if (save >= 1) list = SaveAndReload(list);

            list = list.Add(Alice);

            if (save >= 2) list = SaveAndReload(list);

            Assert.False(list.IsEmpty);
            Assert.Collection(list,
                a1 => Assert.Equal(Bob, a1),
                a2 => Assert.Equal(Alice, a2));

            Assert.Equal(Bob, list[0]);
            Assert.Equal(Alice, list[1]);
        }

        [InlineData(false)]
        [InlineData(true)]
        [Theory]
        public void two_element_addrange(bool save)
        {
            var list = Empty().AddRange(new[] { Bob, Alice });

            if (save) list = SaveAndReload(list);

            Assert.Collection(list,
                a1 => Assert.Equal(Bob, a1),
                a2 => Assert.Equal(Alice, a2));

            Assert.Equal(Bob, list[0]);
            Assert.Equal(Alice, list[1]);
        }

        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [Theory]
        public void one_element_overwrite(int save)
        {
            var list = Empty().Add(Bob);

            if (save >= 1) list = SaveAndReload(list);

            list = list.SetItem(0, Alice);

            if (save >= 2) list = SaveAndReload(list);

            Assert.False(list.IsEmpty);
            Assert.Collection(list,
                a1 => Assert.Equal(Alice, a1));

            Assert.Equal(Alice, list[0]);
        }


        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [Theory]
        public void two_element_overwrite(int save)
        {
            var list = Empty().AddRange(new[] { Bob, Charlie });

            if (save >= 1) list = SaveAndReload(list);

            list = list.SetItem(0, Alice);

            if (save >= 2) list = SaveAndReload(list);

            Assert.False(list.IsEmpty);
            Assert.Collection(list,
                a1 => Assert.Equal(Alice, a1),
                a2 => Assert.Equal(Charlie, a2));

            Assert.Equal(Alice, list[0]);
            Assert.Equal(Charlie, list[1]);
        }

        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [Theory]
        public void three_element_overwrite(int save)
        {
            var list = Empty().AddRange(new[] { Alice, Bob, Charlie });

            if (save >= 1) list = SaveAndReload(list);

            list = list.SetItem(1, Charlie);

            if (save >= 2) list = SaveAndReload(list);

            Assert.False(list.IsEmpty);
            Assert.Collection(list,
                a1 => Assert.Equal(Alice, a1),
                a2 => Assert.Equal(Charlie, a2),
                a3 => Assert.Equal(Charlie, a3));

            Assert.Equal(Alice, list[0]);
            Assert.Equal(Charlie, list[1]);
            Assert.Equal(Charlie, list[2]);
        }

        #region Fixture-ish

        [MessagePackObject]
        public sealed class Person
        {
            [Key(0)]
            public int Age { get; set; }

            [Key(1)]
            public string FirstName { get; set; }

            [Key(2)]
            public string LastName { get; set; }

            public override bool Equals(object obj)
            {
                return obj is Person person &&
                       Age == person.Age &&
                       FirstName == person.FirstName &&
                       LastName == person.LastName;
            }

            public override int GetHashCode()
            {
                int hashCode = -258624264;
                hashCode = hashCode * -1521134295 + Age.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FirstName);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(LastName);
                return hashCode;
            }
        }

        private static Person Alice = new Person
        {
            Age = 18,
            FirstName = "Alice",
            LastName = "Smith"
        };

        private static Person Bob = new Person
        {
            Age = 42,
            FirstName = "Bob",
            LastName = "Brown"
        };

        private static Person Charlie = new Person
        {
            Age = 85,
            FirstName = "Charlie",
            LastName = "Campbell"
        };

        private static MessagePackSerializerOptions Options =
            MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);

        private static LargeImmutableList<Person> Empty() =>
            LargeImmutableList<Person>.Empty(Options);

        private LargeImmutableList<Person> SaveAndReload(LargeImmutableList<Person> p)
        {
            var ms = new MemoryStream();
            p.Save(ms);

            var mem = new VolatileMemory(ms.ToArray());
            return LargeImmutableList<Person>.Load(new BigMemoryStream(mem), Options);
        }

        #endregion
    }
}
