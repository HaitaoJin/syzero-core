using AutoMapper;

namespace SyZero.AutoMapper
{
    /// <summary>
    /// AutoMapper 对象映射器实现
    /// </summary>
    public class ObjectMapper : SyZero.ObjectMapper.IObjectMapper
    {
        private readonly IMapper _mapper;

        /// <summary>
        /// 初始化 ObjectMapper
        /// </summary>
        /// <param name="mapper">AutoMapper IMapper 实例</param>
        public ObjectMapper(IMapper mapper)
        {
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <inheritdoc />
        public TDestination Map<TDestination>(object source)
        {
            return _mapper.Map<TDestination>(source);
        }

        /// <inheritdoc />
        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            return _mapper.Map(source, destination);
        }
    }
}
