namespace NukeBuildHelpers.UnitTest
{
    public class UnitTest1
    {
        [Fact]
        public async void Test1()
        {
            Assert.Equal(1, 1);
            await Task.Delay(5000);
            Assert.Equal(1, 1);
        }
    }
}
