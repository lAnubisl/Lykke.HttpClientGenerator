﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Polly.Caching;

namespace Lykke.HttpClientGenerator.Caching
{
    public interface IRemovableAsyncCacheProvider : IAsyncCacheProvider
    {
        Task RemoveAsync(string ket, CancellationToken token);
    }
}
