
namespace IdleCs.ServerCore
{
	public class CorgiServerObjectSingleton<T> : CorgiServerObject where T : CorgiServerObjectSingleton<T>, new()
	{
		private static T _instance;

		protected CorgiServerObjectSingleton()
		{
		}

		public static T Instance
		{
			get
			{
				if (null == _instance)
				{
					_instance = Nested.Inst;
				}

				return _instance;
			}
		}
		
		public static void InitSingleton(T instance)
		{
			if (instance != null)
			{
				_instance = instance;
			}
		}

		public bool InitSingleton()
		{
			return Init();
		}

		protected virtual bool Init()
		{
			return true;
		}
        
		private static class Nested
		{
			// Explicit static constructor to tell C# compiler
			// not to mark type as beforefieldinit
			static Nested()
			{
			}

			internal static readonly T Inst = new T();
		}
	}
}