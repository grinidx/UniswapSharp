using UniswapSharp.V3.Entities;

namespace UniswapSharp.Testing.V3.Entities;

public class TickDataProviderTests
{
    public class NoTickDataProviderTests
    {
        private readonly NoTickDataProvider _provider = new();

        [Fact]
        public async Task GetTick_ThrowsException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.GetTick(0));
        }

        [Fact]
        public async Task NextInitializedTickWithinOneWord_ThrowsException()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.NextInitializedTickWithinOneWord(0, false, 1));
        }
    }
}
