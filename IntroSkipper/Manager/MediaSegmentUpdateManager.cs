using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IntroSkipper.Data;
using MediaBrowser.Controller;
using MediaBrowser.Model;
using Microsoft.Extensions.Logging;

namespace IntroSkipper.Manager
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MediaSegmentUpdateManager" /> class.
    /// </summary>
    /// <param name="mediaSegmentManager">MediaSegmentManager.</param>
    /// <param name="logger">logger.</param>
    /// <param name="segmentProvider">segmentProvider.</param>
    public class MediaSegmentUpdateManager(IMediaSegmentManager mediaSegmentManager, ILogger<MediaSegmentUpdateManager> logger, IMediaSegmentProvider segmentProvider)
    {
        private readonly IMediaSegmentManager _mediaSegmentManager = mediaSegmentManager;
        private readonly ILogger<MediaSegmentUpdateManager> _logger = logger;
        private readonly IMediaSegmentProvider _segmentProvider = segmentProvider;
        private readonly string _name = Plugin.Instance!.Name;

        /// <summary>
        /// Updates all media items in a List.
        /// </summary>
        /// <param name="episodes">Queued media items.</param>
        /// <param name="cancellationToken">CancellationToken.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task UpdateMediaSegmentsAsync(IReadOnlyList<QueuedEpisode> episodes, CancellationToken cancellationToken)
        {
            foreach (var episode in episodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var existingSegments = await _mediaSegmentManager.GetSegmentsAsync(episode.EpisodeId, null).ConfigureAwait(false);
                    await Task.WhenAll(existingSegments.Select(s => _mediaSegmentManager.DeleteSegmentAsync(s.Id))).ConfigureAwait(false);

                    var newSegments = await _segmentProvider.GetMediaSegments(new MediaSegmentGenerationRequest { ItemId = episode.EpisodeId }, cancellationToken).ConfigureAwait(false);

                    if (newSegments.Count == 0)
                    {
                        _logger.LogDebug("No segments found for episode {EpisodeId}", episode.EpisodeId);
                        continue;
                    }

                    await Task.WhenAll(newSegments.Select(s => _mediaSegmentManager.CreateSegmentAsync(s, _name))).ConfigureAwait(false);

                    _logger.LogDebug("Updated {SegmentCount} segments for episode {EpisodeId}", newSegments.Count, episode.EpisodeId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing episode {EpisodeId}", episode.EpisodeId);
                }
            }
        }
    }
}
