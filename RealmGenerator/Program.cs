﻿namespace RealmGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using AutoMapper;

    using LibGit2Sharp;

    using RealmGenerator.Entities;
    using RealmGenerator.RealmModels;

    using Realms;

    public class Program
    {
        public static void Main(string[] args)
        {
            IList<int> dummy = new List<int>();

            string commitHash;// = "332cff30d041aaf991579f10b5578206e1f28601";

            using (var auditRepo = new Repository(AuditHelper.AuditPath))
            {
                Commands.Checkout(auditRepo, "master");

                Commands.Pull(
                    auditRepo,
                    new Signature("RealmGenerator", "realm", DateTimeOffset.Now),
                    new PullOptions());

                commitHash = auditRepo.Commits.First().Sha;

                Commands.Checkout(auditRepo, commitHash);
            }

            var auditVersion = new AuditVersion { CommitHash = commitHash };

            string realmDirectoryPath = $@"C:\Users\{Environment.UserName}\Source\Repos\App\DotNetRu.DataStore.Audit";
            CleanDirectory(realmDirectoryPath);

            var config = new RealmConfiguration(Path.Combine(realmDirectoryPath, "Audit.realm"));

            var realm = Realm.GetInstance(config);

            realm.Write(() => { realm.Add(auditVersion); });

            InitializeAudoMapper(realm);

            realm.AddEntities<SpeakerEntity, Speaker>("speakers");
            realm.AddEntities<FriendEntity, Friend>("friends");
            realm.AddEntities<VenueEntity, Venue>("venues");
            realm.AddEntities<CommunityEntity, Community>("communities");
            realm.AddEntities<TalkEntity, Talk>("talks");
            realm.AddEntities<MeetupEntity, Meetup>("meetups");
        }

        private static void InitializeAudoMapper(Realm realm)
        {
            Mapper.Initialize(
                cfg =>
                {
                    cfg.CreateMap<SpeakerEntity, Speaker>().AfterMap(
                        (src, dest) =>
                        {
                            dest.Avatar = AuditHelper.LoadImage("speakers", src.Id, "avatar.jpg");
                        });
                    cfg.CreateMap<VenueEntity, Venue>();
                    cfg.CreateMap<FriendEntity, Friend>().AfterMap(
                        (src, dest) =>
                        {
                            var friendId = src.Id;

                            dest.LogoSmall = AuditHelper.LoadImage("friends", friendId, "logo.small.png");
                            dest.Logo = AuditHelper.LoadImage("friends", friendId, "logo.png");
                        });
                    cfg.CreateMap<CommunityEntity, Community>();
                    cfg.CreateMap<TalkEntity, Talk>().AfterMap(
                        (src, dest) =>
                        {
                            foreach (string speakerId in src.SpeakerIds)
                            {
                                var speaker = realm.Find<Speaker>(speakerId);

                                dest.Speakers.Add(speaker);
                            }
                        });
                    cfg.CreateMap<SessionEntity, Session>();
                    cfg.CreateMap<MeetupEntity, Meetup>().AfterMap(
                        (src, dest) =>
                        {
                            realm.Write(() =>
                            {
                                foreach (var session in src.Sessions)
                                {
                                    var realmSession = Mapper.Map<Session>(session);
                                    var talk = realm.Find<Talk>(session.TalkId);

                                    realmSession.Talk = talk;
                                    talk.Session = realmSession;
                                    realmSession.Meetup = dest;
                                    
                                    var s = realm.Add(realmSession);
                                    dest.Sessions.Add(s);

                                }

                                foreach (string friendId in src.FriendIds)
                                {
                                    var friend = realm.Find<Friend>(friendId);
                                    dest.Friends.Add(friend);
                                }

                                dest.Venue = realm.Find<Venue>(src.VenueId);
                            });
                        });
                });
        }

        private static void CleanDirectory(string realmDirectoryPath)
        {
            File.Delete(Path.Combine(realmDirectoryPath, "Audit.realm"));
            File.Delete(Path.Combine(realmDirectoryPath, "Audit.realm.lock"));
        }
    }
}
