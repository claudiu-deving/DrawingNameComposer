using DrawingNameComposer;
using DrawingNameComposer.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions.Collections;
using FluentAssertions.Common;
using FluentAssertions;

namespace DrawingNameComposerTests
{
	[TestClass]
	public class MainViewModelTests
	{
		private readonly MainViewModel _sut;
		private readonly Mock<IMetadataService> _mockedMetadataService;
		private readonly Mock<IPrintingSettingsService> _mockedPrintingService;
		private readonly Mock<IPresetsService> _mockedPresetsService;

		public MainViewModelTests()
		{
			_mockedMetadataService = new Mock<IMetadataService>();
			_mockedPrintingService = new Mock<IPrintingSettingsService>();
			_mockedPresetsService = new Mock<IPresetsService>();
			_sut = new MainViewModel(_mockedMetadataService.Object, _mockedPrintingService.Object, _mockedPresetsService.Object);
		}

		[TestMethod]
		public async Task IsInitializedAsExpected()
		{
			_mockedMetadataService.Setup(m => m.Get()).ReturnsAsync(() => ["A", "B"]);
			_mockedPresetsService.Setup(preset => preset.LoadFromFile()).Returns(() => [ new(){
			Name = "Test",AvailableProperties = ["A","B"],ChosenProperties = ["A"],PrintSetting = "claud"} ]);
			_mockedPresetsService.Setup(preset => preset.FileExists()).Returns(() => true);
			_mockedPrintingService.Setup(print => print.Get()).Returns(["A"]);

			await _sut.Initialize();

			_sut.Presets.Count.Should().Be(1);
			_sut.AvailableProperties.Count.Should().Be(2);
			_sut.ChosenProperties.Count.Should().Be(1);
			_sut.SelectedPreset.Name.Should().Be("Test");
			_sut.SelectedPrintSetting.Should().Be("claud");
		}
	}
}
