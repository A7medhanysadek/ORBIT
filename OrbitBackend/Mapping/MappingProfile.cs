using AutoMapper;
using OrbitBackend.DTOs.Account;
using OrbitBackend.DTOs.Auth;
using OrbitBackend.DTOs.Streaming;
using OrbitBackend.Models;

namespace OrbitBackend.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<RegisterDto, AppUser>()
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Username))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age))
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshToken, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshTokenExpiryTime, opt => opt.Ignore())
                .ForMember(dest => dest.ProfilePictureUrl, opt => opt.Ignore());

            CreateMap<AppUser, AuthResponseDto>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.UserName))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age))
                .ForMember(dest => dest.ProfilePictureUrl, opt => opt.MapFrom(src => src.ProfilePictureUrl))
                .ForMember(dest => dest.AccessToken, opt => opt.Ignore())
                .ForMember(dest => dest.AccessTokenExpiry, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshToken, opt => opt.Ignore())
                .ForMember(dest => dest.RefreshTokenExpiry, opt => opt.Ignore());

            CreateMap<AppUser, RegisterResponseDto>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.UserName))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age))
                .ForMember(dest => dest.Message, opt => opt.Ignore());

            CreateMap<AppUser, AccountDto>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Username, opt => opt.MapFrom(src => src.UserName))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age))
                .ForMember(dest => dest.ProfilePictureUrl, opt => opt.MapFrom(src => src.ProfilePictureUrl))
                .ForMember(dest => dest.EmailConfirmed, opt => opt.MapFrom(src => src.EmailConfirmed))
                .ForMember(dest => dest.Roles, opt => opt.Ignore());

            CreateMap<LiveStream, StreamResponseDto>()
                .ForMember(dest => dest.StreamerName, opt => opt.MapFrom(src => src.Streamer.FullName))
                .ForMember(dest => dest.ChannelName, opt => opt.MapFrom(src => src.Channel.ChannelName))
                .ForMember(dest => dest.ChannelId, opt => opt.MapFrom(src => src.ChannelId))
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
                .ForMember(dest => dest.CategorySlug, opt => opt.MapFrom(src => src.Category != null ? src.Category.Slug : null))
                .ForMember(dest => dest.HlsUrl, opt => opt.Ignore())
                .ForMember(dest => dest.VodUrl, opt => opt.Ignore())
                .ForMember(dest => dest.ViewerCount, opt => opt.Ignore())
                .ForMember(dest => dest.ProfilePictureUrl, opt => opt.MapFrom(src => src.Channel.ProfilePhotoUrl ?? src.Streamer.ProfilePictureUrl));

            CreateMap<LiveStream, LiveStreamSummaryDto>()
                .ForMember(dest => dest.StreamerName, opt => opt.MapFrom(src => src.Streamer.FullName))
                .ForMember(dest => dest.ChannelName, opt => opt.MapFrom(src => src.Channel.ChannelName))
                .ForMember(dest => dest.ChannelId, opt => opt.MapFrom(src => src.ChannelId))
                .ForMember(dest => dest.CategoryId, opt => opt.MapFrom(src => src.CategoryId))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category != null ? src.Category.Name : null))
                .ForMember(dest => dest.CategorySlug, opt => opt.MapFrom(src => src.Category != null ? src.Category.Slug : null))
                .ForMember(dest => dest.HlsUrl, opt => opt.Ignore())
                .ForMember(dest => dest.IsReconnecting, opt => opt.Ignore())
                .ForMember(dest => dest.ViewerCount, opt => opt.Ignore())
                .ForMember(dest => dest.ProfilePictureUrl, opt => opt.MapFrom(src => src.Channel.ProfilePhotoUrl ?? src.Streamer.ProfilePictureUrl));
        }
    }
}
