﻿#region Using Statements
using AutoMapper;
using DataLayer;
using DataLayer.Entities;
using DataLayer.Entities.HomeDevices;
using ServiceLayer.DTO;
using SmartHome.BusinessEntities;
using SmartHome.BusinessEntities.BusinessHomeDevice;
using SmartHome.Products;
using System;
using System.Collections.Generic;
using System.Linq;
#endregion

namespace ServiceLayer
{
    public class HomeDeviceService
    {
        public int AddHomeDevice(string nameHomeDevice, string homeDeviceType)
        {
            HomeDevice homeDevice = BusinessHomeDevice.CreateHomeDevice(homeDeviceType);
            homeDevice.Name = nameHomeDevice;

            using (UnitOfWork repository = new UnitOfWork())
            {
                homeDevice = repository.HomeDeviceRespository.Insert(homeDevice);
                repository.Commit();
            }

            return homeDevice.Id;
        }

        public string[] GetHomeDeviceTypes()
        {
            return HomeDevice.HomeDeviceTypes;
        }

        public void RemoveHomeDevice(int idHomeDevice)
        {
            using (UnitOfWork repository = new UnitOfWork())
            {
                HomeDevice homeDevice = repository.HomeDeviceRespository.GetById(idHomeDevice);

                if (homeDevice == null)
                    return;

                if (homeDevice.InUse)
                {
                    //UPDATE CHECKSUM
                    homeDevice.Connector.Node.UpdateChecksum(null);

                    Services.NodeService.UnlinkHomeDevice(idHomeDevice);
                }

                repository.HomeDeviceRespository.Delete(homeDevice);

                repository.Commit();
            }
        }

        public void SetNameHomeDevice(int idHomeDevice, string newName)
        {
            using (UnitOfWork repository = new UnitOfWork())
            {
                HomeDevice homeDevice = repository.HomeDeviceRespository.GetById(idHomeDevice);

                if (homeDevice == null)
                    return;

                homeDevice.Name = newName;

                repository.Commit();
            }
        }

        /// <param name="x">Relative position 0 to 1 of the X axis</param>
        /// <param name="y">Relative position 0 to 1 of the Y axis</param>
        public void UpdateLocation(int idLocation, float x, float y)
        {
            using (UnitOfWork repository = new UnitOfWork())
            {
                Location location = repository.LocationRepository.GetById(idLocation);

                if (location == null)
                    return;

                location.X = x;
                location.Y = y;

                repository.Commit();
            }
        }

        public IEnumerable<LocationDTO> GetHomeDeviceLocations(int idHomeDevice)
        {
            using (UnitOfWork repository = new UnitOfWork())
            {
                var homeDevice = repository.HomeDeviceRespository.GetById(idHomeDevice);

                if (homeDevice == null)
                    return null;

                return Mapper.Map<IEnumerable<LocationDTO>>(homeDevice.Location);
            }
        }

        public IEnumerable<HomeDeviceDTO> GetAllHomeDevices()
        {
            using (UnitOfWork repository = new UnitOfWork())
            {
                var homeDevices = repository.HomeDeviceRespository.GetAll();

                return Mapper.Map<IEnumerable<HomeDeviceDTO>>(homeDevices);
            }
        }

        public IEnumerable<HomeDeviceDTO> GetAllHomeDevices(bool inUse)
        {
            return GetAllHomeDevices().Where(hd => hd.InUse == inUse);
        }

        public IEnumerable<HomeDeviceDTO> GetAllHomeDevicesInAView(int idView)
        {
            using (UnitOfWork repository = new UnitOfWork())
            {
                if (repository.ViewRepository.GetById(idView) == null)
                    return null;

                var homeDevices = repository.HomeDeviceRespository.GetHomeDevicesWithLocations()
                    .Where(hd => hd.Location.Any(l => l.View.Id == idView));

                return Mapper.Map<IEnumerable<HomeDeviceDTO>>(homeDevices);
            }
        }

        public IEnumerable<HomeDeviceDTO> GetHomeDevicesInAView(int idView, List<string> homeDeviceTypes)
        {
            return GetAllHomeDevicesInAView(idView)
                .Where(hd => homeDeviceTypes.Contains(hd.Type));
        }

        public string[] GetNameProducts()
        {
            return BusinessProduct.GetProducts.Select(p => p.Name).ToArray();
        }
    }
}