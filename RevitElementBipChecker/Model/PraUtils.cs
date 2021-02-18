﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitElementBipChecker.Model
{
    public static class PraUtils
    {
        public const string Caption = "Built-in Parameter Checker";
        /// <summary>
        /// Return Element Selected
        /// </summary>
        /// <param name="_uidoc"></param>
        /// <returns></returns>
        public static List<Element> GetSelection(this UIDocument _uidoc)
        {
            Document doc = _uidoc.Document;
            List<Element> value = new List<Element>();
            _uidoc.Selection.GetElementIds();
            Type t = _uidoc.Selection.GetType();
            if (t.GetMethod("GetElementIds") != null)
            {
                MethodInfo met = t.GetMethod("GetElementIds");
                value = ((ICollection<ElementId>)met.Invoke(_uidoc.Selection, null)).Select(a => doc.GetElement(a)).ToList();
            }
            else
            {
                value = ((System.Collections.IEnumerable)t.GetProperty("Elements").GetValue(_uidoc.Selection, null)).Cast<Element>().ToList();
            }
            return value.OrderBy(x=>x.Name).ToList();
        }

        /// <summary>
        /// MessageBox wrapper for question message.
        /// </summary>
        public static bool QuestionMsg(string msg)
        {
            Debug.WriteLine(msg);

            var dialog = new TaskDialog(Caption);

            dialog.MainIcon = TaskDialogIcon.TaskDialogIconNone;
            dialog.MainInstruction = msg;

            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Instance parameters");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Type parameters");

            return dialog.Show() == TaskDialogResult.CommandLink1;
        }

        public static string RealString(double a)
        {
            return a.ToString("0.##");
        }

        public static string GetParameterType(this Autodesk.Revit.DB.Parameter parameter)
        {
            ParameterType pt = parameter.Definition.ParameterType; // returns 'Invalid' for 'ElementId'
            string s = ParameterType.Invalid == pt ? "" : "/" + pt.ToString();
            return parameter.StorageType.ToString() + s;
            return string.Empty;
        }

        public static string IsReadWrite(this Parameter parameter)
        {
            return parameter.IsReadOnly?"read-only" : "read-write";
        }

        public static string GetValue(this Autodesk.Revit.DB.Parameter _parameter)
        {
            string value;

            switch (_parameter.StorageType)
            {
                // database value, internal units, e.g. feet:
                case StorageType.Double:
                    value = RealString(_parameter.AsDouble());
                    break;
                case StorageType.Integer:
                    value = _parameter.AsInteger().ToString();
                    break;
                case StorageType.String:
                    value = _parameter.AsString();
                    break;
                case StorageType.ElementId:
                    value = _parameter.AsElementId().IntegerValue.ToString();
                    break;
                case StorageType.None:
                    value = "None";
                    break;
                default:
                    Debug.Assert(false, "unexpected storage type"); value = string.Empty;
                    break;
            }

            return value;
        }

        /// <summary>
        /// Helper to return parameter value as string, with additional
        /// support for element id to display the element type referred to.
        /// </summary>
        public static string GetParameterValue2(this Parameter param, Document doc)
        {
            if (param.StorageType == StorageType.ElementId && doc != null)
            {
                var paramId = param.AsElementId();
                var id = paramId.IntegerValue;

                if (id < 0)
                {
                    return id.ToString() + BuiltInCategoryString(id);
                }
                else
                {
                    var element = doc.GetElement(paramId);

                    return ElementDescription(element, true);
                }
            }
            else
            {
                return GetParameterValue(param);
            }
        }

        static int _min_bic = 0;
        static int _max_bic = 0;
        static string BuiltInCategoryString(int id)
        {
            if (_min_bic == 0)
            {
                SetMinAndMaxBuiltInCategory();
            }

            return (_min_bic < id && id < _max_bic) ? " " + ((BuiltInCategory)id).ToString() : string.Empty;
        }
        static void SetMinAndMaxBuiltInCategory()
        {
            var values = Enum.GetValues(typeof(BuiltInCategory));
            _max_bic = values.Cast<int>().Max();
            _min_bic = values.Cast<int>().Min();
        }
        /// <summary>
        /// Return a description string including element id for a given element.
        /// </summary>
        public static string ElementDescription(Element element, bool includeId)
        {
            var description = ElementDescription(element);

            if (includeId)
            {
                description += " " + element.Id.IntegerValue.ToString();
            }

            return description;
        }
        /// <summary>
        /// Return a description string for a given element.
        /// </summary>
        public static string ElementDescription(Element e)
        {
            var description = (null == e.Category) ? e.GetType().Name : e.Category.Name;
            var familyInstance = e as FamilyInstance;

            if (null != familyInstance)
            {
                description += " '" + familyInstance.Symbol.Family.Name + "'";
            }

            if (null != e.Name)
            {
                description += " '" + e.Name + "'";
            }

            return description;
        }

        /// <summary>
        /// Helper to return parameter value as string.
        /// One can also use param.AsValueString() to
        /// get the user interface representation.
        /// </summary>
        public static string GetParameterValue(Parameter param)
        {
            string parameterString;

            switch (param.StorageType)
            {
                case StorageType.Double:
                    //
                    // the internal database unit for all lengths is feet.
                    // for instance, if a given room perimeter is returned as
                    // 102.36 as a double and the display unit is millimeters,
                    // then the length will be displayed as
                    // peri = 102.36220472440
                    // peri * 12 * 25.4
                    // 31200 mm
                    //
                    //s = param.AsValueString(); // value seen by user, in display units
                    //s = param.AsDouble().ToString(); // if not using not using LabUtils.RealString()
                    parameterString = RealString(param.AsDouble()); // raw database value in internal units, e.g. feet
                    break;

                case StorageType.Integer:
                    parameterString = param.AsInteger().ToString();
                    break;

                case StorageType.String:
                    parameterString = param.AsString();
                    break;

                case StorageType.ElementId:
                    parameterString = param.AsElementId().IntegerValue.ToString();
                    break;

                case StorageType.None:
                    parameterString = "?NONE?";
                    break;

                default:
                    parameterString = "?ELSE?";
                    break;
            }

            return parameterString;
        }

        public static string Shared(this Parameter parameter)
        {
            return parameter.IsShared ? "Shared" : "Non-shared";
        }

        public static string Guid(this Parameter parameter)
        {
            return parameter.IsShared ? parameter.GUID.ToString() : string.Empty;
        }
    }
}
