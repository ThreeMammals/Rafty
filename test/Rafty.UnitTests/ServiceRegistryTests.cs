using System;
using System.Collections.Generic;
using Rafty.Infrastructure;
using Rafty.ServiceDiscovery;
using Shouldly;
using TestStack.BDDfy;
using Xunit;

namespace Rafty.UnitTests
{
    public class ServiceRegistryTests
    {
        private RegisterService _registerService;
        private readonly InMemoryServiceRegistry _inMemoryServiceRegistry;
        private List<Service> _result;

        public ServiceRegistryTests()
        {
            _inMemoryServiceRegistry = new InMemoryServiceRegistry();
        }

        [Fact]
        public void can_register_service()
        {
            var registerService = new RegisterService(RaftyServiceDiscoveryName.Get(), Guid.NewGuid(), new Uri("http://www.rafty.co.uk"));

            this.Given(x => GivenAServiceToRegister(registerService))
                .When(x => WhenIRegisterTheService())
                .Then(x => ThenItIsRegistered())
                .BDDfy();
        }

        [Fact]
        public void can_get_all_services_by_key()
        {
            this.Given(x => GivenAListOfRegisteredServices())
               .When(x => WhenIGetTheRegisteredServices())
               .Then(x => ThenTheServicesAreReturned())
               .BDDfy();
        }

        private void GivenAServiceToRegister(RegisterService registerService)
        {
            _registerService = registerService;
        }

        private void WhenIRegisterTheService()
        {
            _inMemoryServiceRegistry.Register(_registerService);
        }

        private void ThenItIsRegistered()
        {
            _inMemoryServiceRegistry.Services.Count.ShouldBe(1);
        }

        private void GivenAListOfRegisteredServices()
        {

            for (int i = 0; i < 5; i++)
            {
                var registerService = new RegisterService(RaftyServiceDiscoveryName.Get(), Guid.NewGuid(), new Uri($"www.rafty.co.uk:123{i}"));
                _inMemoryServiceRegistry.Register(registerService);
            }
        }

        private void WhenIGetTheRegisteredServices()
        {
            _result = _inMemoryServiceRegistry.Get(RaftyServiceDiscoveryName.Get());
        }

        private void ThenTheServicesAreReturned()
        {
            _result.Count.ShouldBe(5);
        }
    }
}
