namespace Ecommerce.Mapping
{
    public class MappingConfigrations : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
           // config.NewConfig<Poll, PollResponse>()
                //.Map(dest => dest.Title, src => src.Summary);
        }
    }
}
