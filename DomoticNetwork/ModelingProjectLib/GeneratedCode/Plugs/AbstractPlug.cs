﻿//------------------------------------------------------------------------------
// <auto-generated>
//     Este código se generó mediante una herramienta.
//     Los cambios del archivo se perderán si se regenera el código.
// </auto-generated>
//------------------------------------------------------------------------------
namespace Plugs
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	public interface AbstractPlug 
	{
		object Description { get;set; }

		object Name { get;set; }

		object Version { get;set; }

		object List Dependencies { get;set; }

		object Author { get;set; }

		object StatePlug { get;set; }

		void Inicialize();

		void Finish();

		void GetFunctions();

		void Invoke();

		void Refresh();

	}
}
