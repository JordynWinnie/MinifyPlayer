﻿using Assets.JsonModels;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.ApplicationStates
{
    public class MainState : State<MainManager>
    {
        private PlaybackState CurrentPlaybackState = new PlaybackState();

        private PlaybackState PreviousPlaybackState = new PlaybackState();

        public MainState(StateMachine<MainManager> SM, MainManager manager) : base(SM, manager)
        {
        }

        public override void Enter()
        {
            AttemptUpdatePlaybackState();
            Manager.UIManager.ShowUI(UIManager.UI.Main);
        }

        public override void Exit()
        {
        }

        public override void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                // Purposely Expire code:
                MainManager.Instance.LoginManager.RefreshToken(false);
            }
        }

        private void AttemptUpdatePlaybackState()
        {
            MainManager.Instance.StartCoroutine(UpdatePlaybackState());
        }

        private IEnumerator UpdatePlaybackState()
        {
            yield return new WaitForSeconds(1f);
            using (var request = MainManager.Instance.GetUnityWebRequestObject("https://api.spotify.com/v1/me/player?additional_types=episode,track", MainManager.RequestMethods.GET))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    MainManager.Instance.ApplicationState.ChangeState(MainManager.Instance.ConnectionErrorState);
                    yield break;
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (request.downloadHandler.text == string.Empty)
                    {
                        //Debug.Log("Spotify Instance Disconnected");
                        AttemptUpdatePlaybackState();
                        yield break;
                    }

                    try
                    {
                        //Gets the Current Playing Type
                        var genericPlaybackState = JsonConvert.DeserializeObject<PlaybackStateGeneric>(
                        request.downloadHandler.text);
                        Debug.Log($"Song Type: {genericPlaybackState.currently_playing_type}");
                        switch (genericPlaybackState.currently_playing_type)
                        {
                            case "unknown":
                                AttemptUpdatePlaybackState();
                                yield break;
                            case "track":
                                var playBackStateSong = JsonConvert.DeserializeObject<PlaybackStateSong>(request.downloadHandler.text);

                                CurrentPlaybackState.SongName = playBackStateSong.item.name;
                                CurrentPlaybackState.AlbumArtURL = playBackStateSong.item.album.images[0].url;

                                break;

                            case "episode":
                                var playBackStatePodcast = JsonConvert.DeserializeObject<PlaybackStatePodcast>(request.downloadHandler.text);

                                CurrentPlaybackState.SongName = playBackStatePodcast.item.name;
                                CurrentPlaybackState.AlbumArtURL = playBackStatePodcast.item.images[0].url;

                                break;

                            default:
                                Debug.LogError($"Song Type not recognised: {genericPlaybackState.currently_playing_type}");
                                break;
                        }

                        var UIManager = MainManager.Instance.UIManager;

                        // If no previous state, set one:
                        if (PreviousPlaybackState.SongName.Equals(string.Empty))
                        {
                            Debug.LogWarning("Set Previous State");
                            UIManager.SetSongName(CurrentPlaybackState.SongName);
                            UIManager.SetAlbumArtURL(CurrentPlaybackState.AlbumArtURL);

                            PreviousPlaybackState.CopyPlaybackState(CurrentPlaybackState);
                            AttemptUpdatePlaybackState();
                            
                            yield break;
                        }

                        // Check for Song Difference, then change
                        if (!CurrentPlaybackState.CheckForDifference(PreviousPlaybackState))
                        {
                            Debug.Log("Song Changed");
                            UIManager.SetSongName(CurrentPlaybackState.SongName);
                            UIManager.SetAlbumArtURL(CurrentPlaybackState.AlbumArtURL);
                            PreviousPlaybackState.CopyPlaybackState(CurrentPlaybackState);
                            
                        }

                        AttemptUpdatePlaybackState();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        AttemptUpdatePlaybackState();
                    }

                    yield break;
                }

                if (request.responseCode == 401)
                {
                    MainManager.Instance.LoginManager.RefreshToken(false);
                    AttemptUpdatePlaybackState();
                    yield break;
                }
            }
        }
    }
}