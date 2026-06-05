using API.Repositories;
using API.Services;


namespace API.Extensions;

public static class CollectionServiceExtension
{
    public static IServiceCollection AddCollectionServices(this IServiceCollection services)
    {
        // Room
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IRoomService, RoomService>();

        // Booking
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingService, BookingService>();

        return services;
    }
}