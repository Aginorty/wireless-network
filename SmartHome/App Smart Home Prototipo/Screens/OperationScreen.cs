﻿using ServiceLayer;
using ServiceLayer.DTO;
using System;
using System.Windows.Forms;
using System.Linq;

namespace App_Smart_Home_Prototipo.Administrator.Screens
{
    public partial class OperationScreen : Form
    {
        public OperationScreen()
        {
            InitializeComponent();

            var homeDevices = Services.HomeDeviceService.GetHomeDevices();

            listBoxHomeDevices.Items.AddRange(homeDevices.ToArray());

            comboBoxToHomeDevice.Items.AddRange(homeDevices.ToArray());
        }

        private void SelectHomeDevice(object sender, EventArgs e)
        {
            HomeDeviceDTO homeDev = (HomeDeviceDTO)listBoxHomeDevices.SelectedItem;

            textBoxNameHomeDevice.Text = homeDev.Name;
            textBoxTypeHomeDevice.Text = homeDev.Type;

            listBoxOperations.Items.Clear();
            listBoxOperations.Items.AddRange(Services.OperationService.GetHomeDeviceOperation(homeDev.Id)); //OJO

            //Clear add operation
            textBoxArgs.Text = "";
            comboBoxOperation.Text = "";
            comboBoxToHomeDevice.Text = "";
        }

        private void RemoveOperation(object sender, EventArgs e)
        {
            OperationDTO operation = (OperationDTO)listBoxOperations.SelectedItem;

            Services.OperationService.RemoveOperation(operation.Id);

            //UpdateOperation
            SelectHomeDevice(this, null);
        }

        private void UpdateOperations(object sender, EventArgs e)
        {
            HomeDeviceDTO homeDev = (HomeDeviceDTO)listBoxHomeDevices.SelectedItem;

            comboBoxOperation.Items.Clear();
            comboBoxOperation.Items.AddRange(Services.OperationService.GetHomeDeviceOperation(homeDev.Id)); //OJO
        }
    }
}
