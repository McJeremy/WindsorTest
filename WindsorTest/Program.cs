using Castle.Core;
using Castle.DynamicProxy;
using Castle.MicroKernel.Registration;
using Castle.MicroKernel.SubSystems.Configuration;
using Castle.Windsor;
using Castle.Windsor.Installer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WindsorTest
{
    class Program
    {
        static void Main(string[] args)
        {
            //Container的使用方式一：直接实例化WindsorContainer
            var container = new WindsorContainer();

            //使用AOP的实践，通过注册事件,
            //当然，也可以直接在相应的类或方法上打上Inteceptor标签。这样就不需要事件
            //比如，下面的UserRepository的实现一样
            container.Kernel.ComponentRegistered += Kernel_ComponentRegistered;
            
            //服务注册方式一：直接使用container.register
            container.Register(Component.For<IRepository>().ImplementedBy<UserRepository>().LifestyleTransient());
            container.Register(Component.For<RepositoryInterceptor1>());
            container.Register(Component.For<RepositoryInterceptor2>().LifestyleTransient().Named("repo2"));

            //服务注册方式二：使用container.Install
            //container.Install(new RepositoryInstaller());
            
            //Container的使用方式二：在方法中实现，并调用installer
            //var container = WindsorBootstraper();

            var user = container.Resolve<IRepository>();
            user.Say();

            container.Dispose();

            Console.Read();
        }

        private static void Kernel_ComponentRegistered(string key, Castle.MicroKernel.IHandler handler)
        {
            //在注册时间中，判断是不是某种类型的，如果是，就添加对应的AOP处理。
            //这种方式按照命名约定来注册
            if (RepositoryAOPHelper.IsConventionalUowClass(handler.ComponentModel.Implementation))
            {
                handler.ComponentModel.Interceptors.Add(new InterceptorReference("repo2"));
                handler.ComponentModel.Interceptors.Add(new InterceptorReference(typeof(RepositoryInterceptor1)));               
            }
            //或者判断有没有打对应的特性标签，如果有打，则添加对应的AOP处理
            else if (handler.ComponentModel.Implementation.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(RepositoryAOPHelper.HasUnitOfWorkAttribute))
            {
                handler.ComponentModel.Interceptors.Add(new InterceptorReference("repo2"));
                handler.ComponentModel.Interceptors.Add(new InterceptorReference(typeof(RepositoryInterceptor1)));
            }
        }

        private static IWindsorContainer WindsorBootstraper()
        {            
           return new WindsorContainer().Install(FromAssembly.This());
        }
    }

    public interface IRepository
    {
        void Say();
    }

    //[RepositoryAOPAttribute("repo2")]
    //[RepositoryAOPAttribute(typeof(RepositoryInterceptor1))]
    public class UserRepository : IRepository
    {
        public void Say()
        {
            Console.WriteLine("UserRepository");
        }
    }

    public class RepositoryInstaller : IWindsorInstaller
    {
        public void Install(IWindsorContainer container, IConfigurationStore store)
        {
            container.Register(Classes.FromThisAssembly().
                BasedOn<IRepository>().
                WithServiceAllInterfaces().
                LifestyleTransient());
            container.Register(Component.For<RepositoryInterceptor1>());
            container.Register(Component.For<RepositoryInterceptor2>().LifestyleTransient().Named("repo2"));
        }
    }

    public class RepositoryInterceptor1 : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            Console.WriteLine("Before repo 1");
            invocation.Proceed();
            Console.WriteLine("after repo 1");
        }
    }

    public class RepositoryInterceptor2 : IInterceptor
    {
        public void Intercept(IInvocation invocation)
        {
            Console.WriteLine("Before repo 2 ");
            invocation.Proceed();
            Console.WriteLine("after repo 2 ");
        }
    }

    [AttributeUsage(AttributeTargets.Class,AllowMultiple =true)]
    public class RepositoryAOPAttribute : InterceptorAttribute
    {
        public RepositoryAOPAttribute(string strKey):base(strKey)
        { }
        
        public RepositoryAOPAttribute(Type type):base(type)
        { }
    }

    internal static class RepositoryAOPHelper
    {
        /// <summary>
        /// Returns true if UOW must be used for given type as convention.
        /// </summary>
        /// <param name="type">Type to check</param>
        public static bool IsConventionalUowClass(Type type)
        {
            return typeof(IRepository).IsAssignableFrom(type);
        }

        /// <summary>
        /// Returns true if given method has UnitOfWorkAttribute attribute.
        /// </summary>
        /// <param name="methodInfo">Method info to check</param>
        public static bool HasUnitOfWorkAttribute(MemberInfo methodInfo)
        {
            return methodInfo.IsDefined(typeof(RepositoryAOPAttribute), true);
        }

        /// <summary>
        /// Returns UnitOfWorkAttribute it exists.
        /// </summary>
        /// <param name="methodInfo">Method info to check</param>
        public static RepositoryAOPAttribute GetUnitOfWorkAttributeOrNull(MemberInfo methodInfo)
        {
            var attrs = methodInfo.GetCustomAttributes(typeof(RepositoryAOPAttribute), false);
            if (attrs.Length <= 0)
            {
                return null;
            }

            return (RepositoryAOPAttribute)attrs[0];
        }
    }
}
